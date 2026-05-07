namespace PriceDiscovery.Application.Services;

using System;
using System.Threading;
using System.Threading.Tasks;

using Polly;
using Polly.CircuitBreaker;
using Polly.Retry;

using PriceDiscovery.Application.Interfaces;
using PriceDiscovery.Domain.Entities;

/// <summary>
/// Circuit Breaker ve Retry policy'leri yöneten sınıf.
/// </summary>
/// <remarks>
/// Mimari Mantık:
/// - Circuit Breaker: Arıza durumunda kaynağı otomatik devre dışı bırakır
/// - Retry: Geçici hatalarda yeniden dener
/// - Fallback: Hata durumunda alternatif davranış
/// - Isolation: Her fetcher kendi policy'sine sahip
/// </remarks>
public class ResilientPriceFetcher : IPriceFetcher
{
    private readonly IPriceFetcher _innerFetcher;
    private readonly CircuitBreakerPolicy _circuitBreakerPolicy;
    private readonly AsyncRetryPolicy _retryPolicy;
    private readonly FetcherConfig _config;
    
    private int _failureCount;
    private DateTime _circuitOpenedAt;
    private bool _isCircuitOpen;
    
    public PriceSource Source => _innerFetcher.Source;
    
    public bool IsEnabled 
    { 
        get => !_isCircuitOpen && _innerFetcher.IsEnabled;
        set => _innerFetcher.IsEnabled = value;
    }
    
    public ResilientPriceFetcher(
        IPriceFetcher innerFetcher,
        CircuitBreakerPolicy circuitBreakerPolicy,
        AsyncRetryPolicy retryPolicy,
        FetcherConfig config)
    {
        _innerFetcher = innerFetcher;
        _circuitBreakerPolicy = circuitBreakerPolicy;
        _retryPolicy = retryPolicy;
        _config = config;
    }
    
    public async Task<PriceQuote?> FetchPriceAsync(
        string symbol,
        CancellationToken cancellationToken = default)
    {
        //Circuit Breaker durumunu kontrol et
        if (_isCircuitOpen)
        {
            if (DateTime.UtcNow - _circuitOpenedAt > _config.CircuitBreakerDuration)
            {
                _isCircuitOpen = false;
                _failureCount = 0;
            }
            else
            {
                return new PriceQuote
                {
                    Symbol = symbol,
                    ErrorMessage = $"Circuit breaker is open for {Source}",
                    Source = Source,
                    Timestamp = DateTime.UtcNow
                };
            }
        }
        
        try
        {
            var result = await _retryPolicy.ExecuteAsync(async () =>
            {
                return await _innerFetcher.FetchPriceAsync(symbol, cancellationToken);
            });
            
            _failureCount = 0;
            
            return result;
        }
        catch (BrokenCircuitException)
        {
            _failureCount++;
            return new PriceQuote
            {
                Symbol = symbol,
                ErrorMessage = $"Circuit breaker is open for {Source}",
                Source = Source,
                Timestamp = DateTime.UtcNow
            };
        }
        catch (Exception ex)
        {
            _failureCount++;
            
            if (_failureCount >= _config.CircuitBreakerFailureThreshold)
            {
                _isCircuitOpen = true;
                _circuitOpenedAt = DateTime.UtcNow;
            }
            
            return new PriceQuote
            {
                Symbol = symbol,
                ErrorMessage = $"Error after {_failureCount} failures: {ex.Message}",
                Source = Source,
                Timestamp = DateTime.UtcNow
            };
        }
    }
    
    public async Task<System.Collections.Generic.IDictionary<string, PriceQuote>> FetchPricesAsync(
        string[] symbols,
        CancellationToken cancellationToken = default)
    {
        return await _innerFetcher.FetchPricesAsync(symbols, cancellationToken);
    }
    
    public async Task<FetcherHealthStatus> CheckHealthAsync(
        CancellationToken cancellationToken = default)
    {
        if (_isCircuitOpen)
        {
            return new FetcherHealthStatus
            {
                IsHealthy = false,
                ErrorMessage = "Circuit breaker is open",
                CheckedAt = DateTime.UtcNow
            };
        }
        
        return await _innerFetcher.CheckHealthAsync(cancellationToken);
    }
}

/// <summary>
/// Polly policy'lerini oluşturan fabrika sınıfı
/// </summary>
public static class PollyPolicyFactory
{
    public static CircuitBreakerPolicy CreateCircuitBreakerPolicy(FetcherConfig config)
    {
        return Policy
            .Handle<Exception>()
            .CircuitBreaker(
                exceptionsAllowedBeforeBreaking: config.CircuitBreakerFailureThreshold,
                durationOfBreak: config.CircuitBreakerDuration,
                onBreak: (exception, duration) =>
                {
                    Console.WriteLine(
                        $"[CircuitBreaker] {config.Source} circuit opened for {duration.TotalSeconds}s. Reason: {exception.Message}");
                },
                onReset: () =>
                {
                    Console.WriteLine($"[CircuitBreaker] {config.Source} circuit reset.");
                },
                onHalfOpen: () =>
                {
                    Console.WriteLine($"[CircuitBreaker] {config.Source} circuit half-open. Testing...");
                });
    }
    
    public static AsyncRetryPolicy CreateRetryPolicy(FetcherConfig config)
    {
        return Policy
            .Handle<Exception>()
            .WaitAndRetryAsync(
                retryCount: config.MaxRetryCount,
                sleepDurationProvider: attempt => TimeSpan.FromSeconds(
                    Math.Pow(2, attempt) * config.RetryMultiplierSeconds),
                onRetry: (exception, timeSpan, retryCount, context) =>
                {
                    Console.WriteLine(
                        $"[Retry] {config.Source} - Attempt {retryCount} failed. Waiting {timeSpan.TotalSeconds}s. Reason: {exception.Message}");
                });
    }
}

/// <summary>
/// Fetcher yapılandırma ayarları
/// </summary>
public class FetcherConfig
{
    public PriceSource Source { get; set; }
    
    public int MaxRetryCount { get; set; } = 3;
    
    public int RetryMultiplierSeconds { get; set; } = 1;
    
    public int CircuitBreakerFailureThreshold { get; set; } = 5;
    
    public TimeSpan CircuitBreakerDuration { get; set; } = TimeSpan.FromSeconds(30);
    
    public int TimeoutSeconds { get; set; } = 10;
    
    public bool EnableCircuitBreaker { get; set; } = true;
    
    public bool EnableRetry { get; set; } = true;
}