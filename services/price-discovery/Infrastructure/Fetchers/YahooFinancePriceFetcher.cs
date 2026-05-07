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
/// Yahoo Finance API'den hisse senedi ve emtia fiyatları çeken fetcher.
/// </summary>
/// <remarks>
/// Mimari Mantık:
/// - Yahoo Finance Rapid API kullanır
/// - Hisse senetleri, ETF'ler ve emtialar için destek
/// - Rate Limiting: Her istek arasında bekleme
/// - Circuit Breaker: Polly ile entegre
/// </remarks>
public class YahooFinancePriceFetcher : IPriceFetcher
{
    private readonly HttpClient _httpClient;
    private readonly YahooFinanceConfig _config;
    
    public PriceSource Source => PriceSource.YahooFinance;
    public bool IsEnabled { get; set; } = true;
    
    public YahooFinancePriceFetcher(HttpClient httpClient, YahooFinanceConfig config)
    {
        _httpClient = httpClient;
        _config = config;
        
        _httpClient.BaseAddress = new Uri(_config.BaseUrl);
        _httpClient.Timeout = TimeSpan.FromSeconds(_config.TimeoutSeconds);
        
        if (!string.IsNullOrEmpty(_config.ApiKey))
        {
            _httpClient.DefaultRequestHeaders.Add("X-Api-Key", _config.ApiKey);
        }
    }
    
    public async Task<PriceQuote?> FetchPriceAsync(
        string symbol,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _httpClient.GetAsync(
                $"/v6/finance/quote?symbols={symbol}",
                cancellationToken);
            
            response.EnsureSuccessStatusCode();
            
            var data = await response.Content.ReadFromJsonAsync<YahooQuoteResponse>(
                cancellationToken: cancellationToken);
            
            if (data?.Result == null || data.Result.Length == 0)
            {
                return new PriceQuote
                {
                    Symbol = symbol,
                    ErrorMessage = "No data returned from Yahoo Finance",
                    Source = Source,
                    Timestamp = DateTime.UtcNow
                };
            }
            
            var quoteData = data.Result[0];
            
            if (quoteData.RegularMarketPrice == 0)
            {
                return new PriceQuote
                {
                    Symbol = symbol,
                    ErrorMessage = "Invalid price (zero)",
                    Source = Source,
                    Timestamp = DateTime.UtcNow
                };
            }
            
            return new PriceQuote
            {
                Symbol = quoteData.Symbol ?? symbol,
                Price = quoteData.RegularMarketPrice,
                BidPrice = quoteData.Bid,
                AskPrice = quoteData.Ask,
                Change24h = quoteData.RegularMarketChangePercent,
                High24h = quoteData.FiftyTwoWeekHigh,
                Low24h = quoteData.FiftyTwoWeekLow,
                Volume24h = quoteData.RegularMarketVolume,
                Timestamp = DateTime.UtcNow,
                Source = Source,
                RawResponse = JsonSerializer.Serialize(quoteData)
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
        
        var commaSeparated = string.Join(",", symbols);
        
        try
        {
            var response = await _httpClient.GetAsync(
                $"/v6/finance/quote?symbols={commaSeparated}",
                cancellationToken);
            
            response.EnsureSuccessStatusCode();
            
            var data = await response.Content.ReadFromJsonAsync<YahooQuoteResponse>(
                cancellationToken: cancellationToken);
            
            if (data?.Result != null)
            {
                foreach (var quote in data.Result)
                {
                    if (!string.IsNullOrEmpty(quote.Symbol))
                    {
                        results[quote.Symbol] = new PriceQuote
                        {
                            Symbol = quote.Symbol,
                            Price = quote.RegularMarketPrice,
                            BidPrice = quote.Bid,
                            AskPrice = quote.Ask,
                            Change24h = quote.RegularMarketChangePercent,
                            High24h = quote.FiftyTwoWeekHigh,
                            Low24h = quote.FiftyTwoWeekLow,
                            Volume24h = quote.RegularMarketVolume,
                            Timestamp = DateTime.UtcNow,
                            Source = Source
                        };
                    }
                }
            }
        }
        catch (Exception ex)
        {
            foreach (var symbol in symbols)
            {
                results[symbol] = new PriceQuote
                {
                    Symbol = symbol,
                    ErrorMessage = $"Batch error: {ex.Message}",
                    Source = Source,
                    Timestamp = DateTime.UtcNow
                };
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
                "/v6/finance/quote?symbols=AAPL",
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
}

/// <summary>
/// Yahoo Finance yapılandırma ayarları
/// </summary>
public class YahooFinanceConfig
{
    public string BaseUrl { get; set; } = "https://apidojo.yahoo.com";
    public int TimeoutSeconds { get; set; } = 10;
    public string? ApiKey { get; set; }
}

/// <summary>
/// Yahoo Finance API yanıt modelleri
/// </summary>
public class YahooQuoteResponse
{
    [JsonPropertyName("quoteResponse")]
    public YahooQuote[]? Result { get; set; }
}

public class YahooQuote
{
    [JsonPropertyName("symbol")]
    public string? Symbol { get; set; }
    
    [JsonPropertyName("shortName")]
    public string? ShortName { get; set; }
    
    [JsonPropertyName("longName")]
    public string? LongName { get; set; }
    
    [JsonPropertyName("quoteType")]
    public string? QuoteType { get; set; }
    
    [JsonPropertyName("exchange")]
    public string? Exchange { get; set; }
    
    [JsonPropertyName("regularMarketPrice")]
    public decimal RegularMarketPrice { get; set; }
    
    [JsonPropertyName("regularMarketChange")]
    public decimal RegularMarketChange { get; set; }
    
    [JsonPropertyName("regularMarketChangePercent")]
    public decimal? RegularMarketChangePercent { get; set; }
    
    [JsonPropertyName("regularMarketDayHigh")]
    public decimal RegularMarketDayHigh { get; set; }
    
    [JsonPropertyName("regularMarketDayLow")]
    public decimal RegularMarketDayLow { get; set; }
    
    [JsonPropertyName("regularMarketVolume")]
    public decimal RegularMarketVolume { get; set; }
    
    [JsonPropertyName("bid")]
    public decimal? Bid { get; set; }
    
    [JsonPropertyName("ask")]
    public decimal? Ask { get; set; }
    
    [JsonPropertyName("bidSize")]
    public int BidSize { get; set; }
    
    [JsonPropertyName("askSize")]
    public int AskSize { get; set; }
    
    [JsonPropertyName("fiftyTwoWeekHigh")]
    public decimal? FiftyTwoWeekHigh { get; set; }
    
    [JsonPropertyName("fiftyTwoWeekLow")]
    public decimal? FiftyTwoWeekLow { get; set; }
    
    [JsonPropertyName("marketCap")]
    public long? MarketCap { get; set; }
    
    [JsonPropertyName("currency")]
    public string? Currency { get; set; }
}