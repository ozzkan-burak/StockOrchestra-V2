namespace PortfolioManager.Infrastructure.Streams;

using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

using StackExchange.Redis;

using PortfolioManager.Application.Interfaces;
using Shared.Events;

/// <summary>
/// Redis Streams Consumer Worker - Fiyat stream'ini dinler ve portföy değerlerini hesaplar.
/// </summary>
/// <remarks>
/// Mimari Mantık:
/// - BackgroundService: Sürekli çalışan arka plan servisi
/// - Consumer Group: Birden fazla instance'ı destekler
/// - Idempotency: EventId kontrolü ile tekrar işlemeyi engeller
/// - Real-time Calculation: Fiyat geldiğinde anında hesaplama
/// </remarks>
public class PriceStreamConsumer : BackgroundService
{
    private readonly IConnectionMultiplexer _redis;
    private readonly ConsumerConfig _config;
    private readonly ILedgerRepository _ledgerRepository;
    private readonly ILogger<PriceStreamConsumer> _logger;
    
    private IdempotencyChecker? _idempotencyChecker;
    private RedisStreamsConsumer? _consumer;
    
    public PriceStreamConsumer(
        IConnectionMultiplexer redis,
        ConsumerConfig config,
        ILedgerRepository ledgerRepository,
        ILogger<PriceStreamConsumer> logger)
    {
        _redis = redis;
        _config = config;
        _ledgerRepository = ledgerRepository;
        _logger = logger;
    }
    
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_config.EnableConsumer)
        {
            _logger.LogInformation("Price stream consumer is disabled");
            return;
        }
        
        var streamsConfig = new RedisStreamsConfig
        {
            PriceStreamName = _config.StreamName,
            DefaultConsumerGroup = _config.ConsumerGroup
        };
        
        _consumer = new RedisStreamsConsumer(_redis, streamsConfig);
        
        try
        {
            await _consumer.CreateConsumerGroupAsync(
                _config.StreamName,
                _config.ConsumerGroup,
                stoppingToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Consumer group may already exist");
        }
        
        var idempotencyConfig = new IdempotencyConfig
        {
            KeyPrefix = _config.IdempotencyKeyPrefix
        };
        _idempotencyChecker = new IdempotencyChecker(_redis, idempotencyConfig);
        
        _logger.LogInformation(
            "Price stream consumer started. Stream: {Stream}, Group: {Group}",
            _config.StreamName,
            _config.ConsumerGroup);
        
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                if (_consumer == null) break;
                
                var events = await _consumer.ConsumeEventsAsync(
                    _config.StreamName,
                    _config.ConsumerGroup,
                    _config.ConsumerName,
                    _config.BatchSize,
                    acknowledge: true,
                    stoppingToken);
                
                foreach (var streamEvent in events)
                {
                    await ProcessEventAsync(streamEvent, stoppingToken);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error consuming events");
            }
            
            await Task.Delay(_config.PollingIntervalMs, stoppingToken);
        }
    }
    
    private async Task ProcessEventAsync(StreamEvent streamEvent, CancellationToken cancellationToken)
    {
        try
        {
            var eventData = JsonSerializer.Deserialize<PriceUpdatedEvent>(streamEvent.EventData);
            
            if (eventData == null)
            {
                _logger.LogWarning("Failed to deserialize event: {MessageId}", streamEvent.MessageId);
                return;
            }
            
            var eventId = eventData.EventId;
            
            if (_idempotencyChecker != null)
            {
                var canProcess = await _idempotencyChecker.TryProcessAsync(eventId, cancellationToken);
                
                if (!canProcess)
                {
                    _logger.LogDebug("Event already processed: {EventId}", eventId);
                    return;
                }
            }
            
            await CalculatePortfolioValueAsync(eventData, cancellationToken);
            
            _logger.LogInformation(
                "Processed price update for {Symbol}: {Price}",
                eventData.Symbol,
                eventData.Price);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing event: {MessageId}", streamEvent.MessageId);
        }
    }
    
    private async Task CalculatePortfolioValueAsync(PriceUpdatedEvent priceEvent, CancellationToken cancellationToken)
    {
        var symbol = priceEvent.Symbol;
        
        var usersWithAsset = await GetUsersWithAssetAsync(symbol, cancellationToken);
        
        foreach (var userId in usersWithAsset)
        {
            var balance = await _ledgerRepository.GetAssetBalanceAsync(userId, Guid.Empty, cancellationToken);
            
            if (balance <= 0) continue;
            
            var totalValue = balance * priceEvent.Price;
            
            await UpdatePortfolioCacheAsync(userId, symbol, priceEvent.Price, balance, totalValue, cancellationToken);
        }
    }
    
    private async Task<List<Guid>> GetUsersWithAssetAsync(string symbol, CancellationToken cancellationToken)
    {
        return new List<Guid>();
    }
    
    private async Task UpdatePortfolioCacheAsync(
        Guid userId,
        string symbol,
        decimal currentPrice,
        decimal balance,
        decimal totalValue,
        CancellationToken cancellationToken)
    {
        var db = _redis.GetDatabase();
        
        var cacheKey = $"portfolio:{userId}:{symbol}";
        
        var cacheData = new
        {
            symbol,
            balance,
            currentPrice,
            totalValue,
            change24h = 0m,
            updatedAt = DateTime.UtcNow
        };
        
        var json = JsonSerializer.Serialize(cacheData);
        
        await db.StringSetAsync(cacheKey, json, _config.CacheTtl);
        
        var portfolioKey = $"portfolio:{userId}:value";
        
        await db.StringIncrementAsync(portfolioKey, (long)(totalValue * 100));
    }
}

/// <summary>
/// Consumer yapılandırması
/// </summary>
public class ConsumerConfig
{
    public string StreamName { get; set; } = "prices:stream";
    
    public string ConsumerGroup { get; set; } = "stockorchestra-consumers";
    
    public string ConsumerName { get; set; } = "portfolio-manager-1";
    
    public bool EnableConsumer { get; set; } = true;
    
    public int BatchSize { get; set; } = 10;
    
    public int PollingIntervalMs { get; set; } = 1000;
    
    public string IdempotencyKeyPrefix { get; set; } = "idempotency:";
    
    public TimeSpan CacheTtl { get; set; } = TimeSpan.FromMinutes(5);
    
    public string RedisConnectionString { get; set; } = "localhost:6379";
}