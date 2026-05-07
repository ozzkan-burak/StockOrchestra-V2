namespace PriceDiscovery;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

using PriceDiscovery.Application.Interfaces;
using PriceDiscovery.Application.Services;
using PriceDiscovery.Domain.Entities;
using PriceDiscovery.Infrastructure.Fetchers;
using PriceDiscovery.Infrastructure.Redis;
using PriceDiscovery.Infrastructure.Streams;

using StackExchange.Redis;

/// <summary>
/// Price Discovery Worker - Arka planda sürekli çalışan fiyat toplama motoru.
/// </summary>
/// <remarks>
/// Mimari Mantık:
/// - BackgroundService: Sürekli çalışan arka plan servisi
/// - Timer-based: Belirli aralıklarla fiyatları toplar
/// - Multi-source: Birden fazla kaynaktan veri çeker
/// - Median: Fiyatların ortancasını hesaplar
/// - Redis: Verileri Redis'e kaydeder ve Pub/Sub yapar
/// - Circuit Breaker: Arızalı kaynakları devre dışı bırakır
/// </remarks>
public class Worker : BackgroundService
{
    private readonly ILogger<Worker> _logger;
    private readonly IServiceProvider _serviceProvider;
    private readonly WorkerConfig _config;
    
    private readonly List<IPriceFetcher> _fetchers = new();
    private MedianPriceDiscoveryService? _medianService;
    private RedisPriceCache? _redisCache;
    private PriceEventPublisher? _publisher;
    
    public Worker(
        ILogger<Worker> logger,
        IServiceProvider serviceProvider,
        WorkerConfig config)
    {
        _logger = logger;
        _serviceProvider = serviceProvider;
        _config = config;
    }
    
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Price Discovery Worker started at: {time}", DateTimeOffset.Now);
        
        await InitializeServicesAsync(stoppingToken);
        
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await DiscoverPricesAsync(stoppingToken);
                
                await PerformHealthChecksAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in price discovery cycle");
            }
            
            await Task.Delay(_config.PollingIntervalMs, stoppingToken);
        }
        
        _logger.LogInformation("Price Discovery Worker stopped at: {time}", DateTimeOffset.Now);
    }
    
    private async Task InitializeServicesAsync(CancellationToken stoppingToken)
    {
        using var scope = _serviceProvider.CreateScope();
        
        var httpClientFactory = scope.ServiceProvider.GetRequiredService<IHttpClientFactory>();
        
        var binanceConfig = new BinanceConfig { BaseUrl = _config.BinanceBaseUrl };
        var binanceHttpClient = httpClientFactory.CreateClient("Binance");
        var binanceFetcher = new BinancePriceFetcher(binanceHttpClient, binanceConfig);
        _fetchers.Add(WrapWithResilience(binanceFetcher, PriceSource.Binance));

        _fetchers.Add(WrapWithResilience(new MockPriceFetcher(), PriceSource.Mock));
        
        var yahooConfig = new YahooFinanceConfig 
        { 
            BaseUrl = _config.YahooFinanceBaseUrl,
            ApiKey = _config.YahooApiKey 
        };
        var yahooHttpClient = httpClientFactory.CreateClient("YahooFinance");
        var yahooFetcher = new YahooFinancePriceFetcher(yahooHttpClient, yahooConfig);
        _fetchers.Add(WrapWithResilience(yahooFetcher, PriceSource.YahooFinance));
        
        var medianConfig = new MedianPriceConfig
        {
            MaxStalenessSeconds = _config.MaxStalenessSeconds,
            MaxPriceDeviationPercent = _config.MaxPriceDeviationPercent
        };
        _medianService = new MedianPriceDiscoveryService(medianConfig);
        
        try
        {
            _logger.LogInformation("Connecting to Redis: {connectionString}", _config.RedisConnectionString);
            var redis = ConnectionMultiplexer.Connect(_config.RedisConnectionString);
            _logger.LogInformation("Redis connection established");
            
            var redisConfig = new RedisCacheConfig
            {
                ConnectionString = _config.RedisConnectionString,
                PriceTtl = TimeSpan.FromSeconds(_config.RedisPriceTtlSeconds)
            };
            _redisCache = new RedisPriceCache(redis, redisConfig);
            _logger.LogInformation("Connected to Redis cache");

            var publisherConfig = new PublisherConfig
            {
                StreamName = "prices:stream"
            };
            _publisher = new PriceEventPublisher(redis, publisherConfig);
            _logger.LogInformation("Initialized PriceEventPublisher for stream: {streamName}", publisherConfig.StreamName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to connect to Redis. Error: {Message}. Running without cache.", ex.Message);
            _redisCache = null;
            _publisher = null;
        }
        
        _logger.LogInformation("Initialized {fetcherCount} fetchers", _fetchers.Count);
    }
    
    private IPriceFetcher WrapWithResilience(IPriceFetcher fetcher, PriceSource source)
    {
        var config = new FetcherConfig
        {
            Source = source,
            MaxRetryCount = _config.MaxRetryCount,
            RetryMultiplierSeconds = _config.RetryMultiplierSeconds,
            CircuitBreakerFailureThreshold = _config.CircuitBreakerFailureThreshold,
            CircuitBreakerDuration = TimeSpan.FromSeconds(_config.CircuitBreakerDurationSeconds),
            EnableCircuitBreaker = _config.EnableCircuitBreaker,
            EnableRetry = _config.EnableRetry
        };
        
        return new ResilientPriceFetcher(
            fetcher,
            PollyPolicyFactory.CreateCircuitBreakerPolicy(config),
            PollyPolicyFactory.CreateRetryPolicy(config),
            config);
    }
    
    private async Task DiscoverPricesAsync(CancellationToken stoppingToken)
    {
        if (_medianService == null)
        {
            return;
        }
        
        var symbolsToFetch = _config.SymbolsToMonitor.ToList();
        
        foreach (var symbol in symbolsToFetch)
        {
            var quotes = new List<PriceQuote>();
            
            if (_config.UseMockData)
            {
                var mockFetcher = new MockPriceFetcher();
                var mockQuote = await mockFetcher.FetchPriceAsync(symbol, stoppingToken);
                if (mockQuote != null)
                {
                    quotes.Add(mockQuote);
                    _logger.LogInformation("Using mock data for {symbol}: {price}", symbol, mockQuote.Price);
                }
            }
            else
            {
                foreach (var fetcher in _fetchers)
                {
                    if (!fetcher.IsEnabled)
                    {
                        continue;
                    }
                    
                    var quote = await fetcher.FetchPriceAsync(symbol, stoppingToken);
                    if (quote != null)
                    {
                        quotes.Add(quote);
                    }
                }
            }
            
            if (quotes.Count == 0)
            {
                _logger.LogWarning("No quotes received for {symbol}", symbol);
                continue;
            }
            
            var discoveredPrice = await _medianService.DiscoverMedianPriceAsync(quotes, stoppingToken);
            
            if (discoveredPrice != null && _redisCache != null)
            {
                try
                {
                    _logger.LogDebug("Storing price for {symbol}: {price}", discoveredPrice.Symbol, discoveredPrice.Price);
                    
                    await _redisCache.SetDiscoveredPriceAsync(discoveredPrice, stoppingToken);
                    
                    var stored = await _redisCache.GetDiscoveredPriceAsync(discoveredPrice.Symbol, stoppingToken);
                    _logger.LogDebug("Verified stored price for {symbol}: {price}", stored?.Symbol, stored?.Price);
                    
                    if (_config.EnablePubSub)
                    {
                        await _redisCache.PublishPriceChangeAsync(discoveredPrice, stoppingToken);
                        _logger.LogDebug("Published pubsub for {symbol}", discoveredPrice.Symbol);
                    }

                    if (_publisher != null)
                    {
                        await _publisher.PublishPriceUpdatedAsync(discoveredPrice, stoppingToken);
                        _logger.LogDebug("Published stream for {symbol}", discoveredPrice.Symbol);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error storing price for {symbol}", discoveredPrice.Symbol);
                }
                
                _logger.LogInformation(
                    "Discovered price for {symbol}: {price} (from {validCount}/{totalCount} sources)",
                    symbol,
                    discoveredPrice.Price,
                    discoveredPrice.ValidSourceCount,
                    discoveredPrice.SourceCount);
            }
        }
    }
    
    private async Task PerformHealthChecksAsync(CancellationToken stoppingToken)
    {
        if (_redisCache == null)
        {
            return;
        }
        
        foreach (var fetcher in _fetchers)
        {
            var health = await fetcher.CheckHealthAsync(stoppingToken);
            
            await _redisCache.SetFetcherHealthAsync(fetcher.Source, health, stoppingToken);
            
            if (!health.IsHealthy)
            {
                _logger.LogWarning("Fetcher {source} is unhealthy: {error}", fetcher.Source, health.ErrorMessage);
            }
        }
    }
}

/// <summary>
/// Worker yapılandırma ayarları
/// </summary>
public class WorkerConfig
{
    public int PollingIntervalMs { get; set; } = 5000;
    
    public List<string> SymbolsToMonitor { get; set; } = new() { "BTC", "ETH", "AAPL", "GOOGL", "TSLA" };
    
    public string BinanceBaseUrl { get; set; } = "https://api.binance.com";
    
    public string YahooFinanceBaseUrl { get; set; } = "https://apidojo.yahoo.com";
    
    public string? YahooApiKey { get; set; }
    
    public string RedisConnectionString { get; set; } = "localhost:6379,password=redis_secure_pass_2024";
    
    public bool UseMockData { get; set; } = true;
    
    public int RedisPriceTtlSeconds { get; set; } = 300;
    
    public int MaxStalenessSeconds { get; set; } = 60;
    
    public decimal MaxPriceDeviationPercent { get; set; } = 10;
    
    public bool EnableCircuitBreaker { get; set; } = true;
    
    public bool EnableRetry { get; set; } = true;
    
    public int MaxRetryCount { get; set; } = 3;
    
    public int RetryMultiplierSeconds { get; set; } = 1;
    
    public int CircuitBreakerFailureThreshold { get; set; } = 5;
    
    public int CircuitBreakerDurationSeconds { get; set; } = 30;
    
    public bool EnablePubSub { get; set; } = true;
}