namespace PriceDiscovery.Infrastructure.Fetchers;

using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;

using PriceDiscovery.Application.Interfaces;
using PriceDiscovery.Domain.Entities;

/// <summary>
/// Binance API'den kripto para fiyatları çeken fetcher.
/// </summary>
/// <remarks>
/// Mimari Mantık:
/// - REST API: Binance REST API kullanır
/// - Rate Limiting: Binance rate limit'lerine uyum sağlar
/// - Circuit Breaker: Polly ile entegre
/// - Async: Tamamen async/await, blocking yok
/// </remarks>
public class BinancePriceFetcher : IPriceFetcher
{
    private readonly HttpClient _httpClient;
    private readonly BinanceConfig _config;
    
    public PriceSource Source => PriceSource.Binance;
    public bool IsEnabled { get; set; } = true;
    
    public BinancePriceFetcher(HttpClient httpClient, BinanceConfig config)
    {
        _httpClient = httpClient;
        _config = config;
        
        _httpClient.BaseAddress = new Uri(_config.BaseUrl);
        _httpClient.Timeout = TimeSpan.FromSeconds(_config.TimeoutSeconds);
    }
    
    public async Task<PriceQuote?> FetchPriceAsync(
        string symbol,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var pairSymbol = NormalizeSymbol(symbol, "USDT");
            
            var response = await _httpClient.GetAsync(
                $"/api/v3/ticker/24hr?symbol={pairSymbol}",
                cancellationToken);
            
            response.EnsureSuccessStatusCode();
            
            var data = await response.Content.ReadFromJsonAsync<BinanceTickerResponse>(
                cancellationToken: cancellationToken);
            
            if (data == null || string.IsNullOrEmpty(data.Symbol))
            {
                return new PriceQuote
                {
                    Symbol = symbol,
                    ErrorMessage = "Empty response from Binance",
                    Source = Source,
                    Timestamp = DateTime.UtcNow
                };
            }
            
            return new PriceQuote
            {
                Symbol = NormalizeSymbolBack(data.Symbol),
                Price = data.LastPrice,
                BidPrice = data.BidPrice,
                AskPrice = data.AskPrice,
                Change24h = data.PriceChangePercent,
                High24h = data.HighPrice,
                Low24h = data.LowPrice,
                Volume24h = data.Volume,
                Timestamp = DateTime.UtcNow,
                Source = Source,
                RawResponse = JsonSerializer.Serialize(data)
            };
        }
        catch (HttpRequestException ex)
        {
            return new PriceQuote
            {
                Symbol = symbol,
                ErrorMessage = $"HTTP Error: {ex.Message}",
                Source = Source,
                Timestamp = DateTime.UtcNow
            };
        }
        catch (TaskCanceledException ex) when (ex.CancellationToken != cancellationToken)
        {
            return new PriceQuote
            {
                Symbol = symbol,
                ErrorMessage = "Request timeout",
                Source = Source,
                Timestamp = DateTime.UtcNow
            };
        }
        catch (Exception ex)
        {
            return new PriceQuote
            {
                Symbol = symbol,
                ErrorMessage = $"Unexpected error: {ex.Message}",
                Source = Source,
                Timestamp = DateTime.UtcNow
            };
        }
    }
    
    public async Task<IDictionary<string, PriceQuote>> FetchPricesAsync(
        string[] symbols,
        CancellationToken cancellationToken = default)
    {
        var results = new Dictionary<string, PriceQuote>();
        
        var tasks = new List<Task<PriceQuote?>>();
        
        foreach (var symbol in symbols)
        {
            tasks.Add(FetchPriceAsync(symbol, cancellationToken));
        }
        
        var quotes = await Task.WhenAll(tasks);
        
        for (int i = 0; i < symbols.Length; i++)
        {
            var quote = quotes[i];
            if (quote != null)
            {
                results[symbols[i]] = quote;
            }
        }
        
        return results;
    }
    
    public async Task<FetcherHealthStatus> CheckHealthAsync(
        CancellationToken cancellationToken = default)
    {
        var startTime = DateTime.UtcNow;
        
        try
        {
            var response = await _httpClient.GetAsync(
                "/api/v3/ping",
                cancellationToken);
            
            var latency = DateTime.UtcNow - startTime;
            
            return new FetcherHealthStatus
            {
                IsHealthy = response.IsSuccessStatusCode,
                Latency = latency,
                CheckedAt = DateTime.UtcNow
            };
        }
        catch (Exception ex)
        {
            return new FetcherHealthStatus
            {
                IsHealthy = false,
                ErrorMessage = ex.Message,
                CheckedAt = DateTime.UtcNow
            };
        }
    }
    
    private string NormalizeSymbol(string symbol, string quote = "USDT")
    {
        var upperSymbol = symbol.ToUpperInvariant();
        
        var symbolMapping = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { "XAUUSD", "XAUUSDT" },
            { "XAU", "XAUUSDT" },
            { "GOLD", "XAUUSDT" }
        };
        
        if (symbolMapping.TryGetValue(upperSymbol, out var mappedSymbol))
        {
            return mappedSymbol;
        }
        
        if (!upperSymbol.EndsWith(quote))
        {
            upperSymbol += quote;
        }
        return upperSymbol;
    }
    
    private string NormalizeSymbolBack(string symbol)
    {
        if (symbol.EndsWith("USDT"))
        {
            return symbol[..^4];
        }
        return symbol;
    }
}

/// <summary>
/// Binance yapılandırma ayarları
/// </summary>
public class BinanceConfig
{
    public string BaseUrl { get; set; } = "https://api.binance.com";
    public int TimeoutSeconds { get; set; } = 10;
    public string? ApiKey { get; set; }
    public string? ApiSecret { get; set; }
}

/// <summary>
/// Binance 24 saatlik ticker yanıtı
/// </summary>
public class BinanceTickerResponse
{
    [JsonPropertyName("symbol")]
    public string Symbol { get; set; } = string.Empty;
    
    [JsonPropertyName("priceChange")]
    public decimal PriceChange { get; set; }
    
    [JsonPropertyName("priceChangePercent")]
    public decimal? PriceChangePercent { get; set; }
    
    [JsonPropertyName("lastPrice")]
    public decimal LastPrice { get; set; }
    
    [JsonPropertyName("bidPrice")]
    public decimal BidPrice { get; set; }
    
    [JsonPropertyName("askPrice")]
    public decimal AskPrice { get; set; }
    
    [JsonPropertyName("highPrice")]
    public decimal HighPrice { get; set; }
    
    [JsonPropertyName("lowPrice")]
    public decimal LowPrice { get; set; }
    
    [JsonPropertyName("volume")]
    public decimal Volume { get; set; }
    
    [JsonPropertyName("quoteVolume")]
    public decimal QuoteVolume { get; set; }
    
    [JsonPropertyName("openPrice")]
    public decimal OpenPrice { get; set; }
    
    [JsonPropertyName("openTime")]
    public long OpenTime { get; set; }
    
    [JsonPropertyName("closeTime")]
    public long CloseTime { get; set; }
    
    [JsonPropertyName("firstId")]
    public long FirstId { get; set; }
    
    [JsonPropertyName("lastId")]
    public long LastId { get; set; }
    
    [JsonPropertyName("count")]
    public long Count { get; set; }
}