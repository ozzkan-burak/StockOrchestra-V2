namespace AnalyticalStore;

using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

using StackExchange.Redis;

using AnalyticalStore.Domain.Entities;
using AnalyticalStore.Infrastructure.Data;
using Shared.Events;

/// <summary>
/// Analytical Store Worker - Redis Streams'ten fiyatları okur ve TimescaleDB'ye yazar.
/// </summary>
/// <remarks>
/// Mimari Mantık:
/// - BackgroundService: Sürekli çalışan arka plan servisi
/// - Redis Consumer: prices:stream'den veri okur
/// - TimescaleDB Writer: Verileri zaman serisi tablosuna yazar
/// - Idempotency: Tekrar işlemeyi engeller
/// - Bulk Insert: Toplu yazma ile performans
/// </remarks>
public class Worker : BackgroundService
{
    private readonly ILogger<Worker> _logger;
    private readonly WorkerConfig _config;
    private readonly PriceRepository _repository;
    private readonly IConnectionMultiplexer _redis;
    
    private RedisStreamsConsumer? _consumer;
    private IdempotencyChecker? _idempotencyChecker;
    
    public Worker(
        ILogger<Worker> logger,
        WorkerConfig config,
        PriceRepository repository,
        IConnectionMultiplexer redis)
    {
        _logger = logger;
        _config = config;
        _repository = repository;
        _redis = redis;
    }
    
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_config.EnableConsumer)
        {
            _logger.LogInformation("Analytical Store consumer is disabled");
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
            "Analytical Store started. Stream: {Stream}, Group: {Group}",
            _config.StreamName,
            _config.ConsumerGroup);
        
        var batch = new List<PriceRecord>();
        var lastFlush = DateTime.UtcNow;
        
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
                    acknowledge: false,
                    stoppingToken);
                
                foreach (var streamEvent in events)
                {
                    var priceRecord = await ProcessEventAsync(streamEvent, stoppingToken);
                    
                    if (priceRecord != null)
                    {
                        batch.Add(priceRecord);
                    }
                    
                    if (batch.Count >= _config.BatchSize ||
                        (DateTime.UtcNow - lastFlush).TotalSeconds >= _config.FlushIntervalSeconds)
                    {
                        await FlushBatchAsync(batch, stoppingToken);
                        batch.Clear();
                        lastFlush = DateTime.UtcNow;
                    }
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                if (batch.Count > 0)
                {
                    await FlushBatchAsync(batch, stoppingToken);
                }
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error consuming events");
            }
            
            await Task.Delay(_config.PollingIntervalMs, stoppingToken);
        }
    }
    
    private async Task<PriceRecord?> ProcessEventAsync(StreamEvent streamEvent, CancellationToken cancellationToken)
    {
        try
        {
            var eventData = JsonSerializer.Deserialize<PriceUpdatedEvent>(streamEvent.EventData);
            
            if (eventData == null)
            {
                _logger.LogWarning("Failed to deserialize event: {MessageId}", streamEvent.MessageId);
                return null;
            }
            
            var eventId = eventData.EventId;
            
            if (_idempotencyChecker != null)
            {
                var canProcess = await _idempotencyChecker.TryProcessAsync(eventId, cancellationToken);
                
                if (!canProcess)
                {
                    _logger.LogDebug("Event already processed: {EventId}", eventId);
                    return null;
                }
            }
            
            var sourcesJson = JsonSerializer.Serialize(eventData.Sources);
            
            return new PriceRecord
            {
                CreatedAt = eventData.DiscoveredAt == default ? DateTime.UtcNow : eventData.DiscoveredAt,
                Symbol = eventData.Symbol,
                AssetId = eventData.AssetId,
                CurrentPrice = eventData.Price,
                BidPrice = eventData.BidPrice,
                AskPrice = eventData.AskPrice,
                Change24h = eventData.Change24h,
                Sources = sourcesJson,
                ValidSourceCount = eventData.ValidSourceCount,
                SourceTimestamp = eventData.Timestamp,
                ValidationStatus = "valid"
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing event: {MessageId}", streamEvent.MessageId);
            return null;
        }
    }
    
    private async Task FlushBatchAsync(List<PriceRecord> batch, CancellationToken cancellationToken)
    {
        if (batch.Count == 0) return;
        
        try
        {
            var inserted = await _repository.InsertPricesBulkAsync(batch, cancellationToken);
            
            _logger.LogInformation(
                "Inserted {Count} prices to TimescaleDB",
                inserted);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to insert batch of {Count} prices", batch.Count);
        }
    }
}

/// <summary>
/// Worker yapılandırması
/// </summary>
public class WorkerConfig
{
    public string StreamName { get; set; } = "prices:stream";
    
    public string ConsumerGroup { get; set; } = "stockorchestra-consumers";
    
    public string ConsumerName { get; set; } = "analytical-store-1";
    
    public bool EnableConsumer { get; set; } = true;
    
    public int BatchSize { get; set; } = 100;
    
    public int FlushIntervalSeconds { get; set; } = 5;
    
    public int PollingIntervalMs { get; set; } = 1000;
    
    public string IdempotencyKeyPrefix { get; set; } = "idempotency:";
    
    public string PostgresConnectionString { get; set; } = "Host=localhost;Database=stockorchestra_ledger;Username=stockorchestra;Password=stockorchestra_secure_pass_2024";
    
    public string RedisConnectionString { get; set; } = "localhost:6379,password=redis_secure_pass_2024";
}