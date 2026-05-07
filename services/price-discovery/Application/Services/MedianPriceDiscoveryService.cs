namespace PriceDiscovery.Application.Services;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using PriceDiscovery.Application.Interfaces;
using PriceDiscovery.Domain.Entities;

/// <summary>
/// Median Price Discovery Service - Birden fazla kaynaktan gelen fiyatların ortancasını hesaplar.
/// </summary>
/// <remarks>
/// Mimari Mantık:
/// - Median (Ortanca): Aşırı değerlere karşı daha dayanıklı
/// - Staleness Check: Çok eski verileri filtreler
/// - Price Deviation: Aşırı sapma gösteren fiyatları elemine eder
/// - Async: Tamamen async/await
/// </remarks>
public class MedianPriceDiscoveryService
{
    private readonly MedianPriceConfig _config;
    
    public MedianPriceDiscoveryService(MedianPriceConfig config)
    {
        _config = config;
    }
    
    /// <summary>
    /// Birden fazla kaynaktan gelen fiyatların median değerini hesaplar.
    /// </summary>
    /// <param name="quotes">Kaynaklardan gelen fiyat teklifleri</param>
    /// <param name="cancellationToken">İptal belirteci</param>
    /// <returns>Median fiyat bilgisi veya null</returns>
    public async Task<DiscoveredPrice?> DiscoverMedianPriceAsync(
        IEnumerable<PriceQuote> quotes,
        CancellationToken cancellationToken = default)
    {
        var quoteList = quotes?.ToList() ?? new List<PriceQuote>();
        
        if (quoteList.Count == 0)
        {
            return null;
        }
        
        var validQuotes = new List<PriceQuote>();
        var rejectedReasons = new Dictionary<string, string>();
        
        foreach (var quote in quoteList)
        {
            var validation = await ValidateQuoteAsync(quote, cancellationToken);
            
            if (validation.IsValid)
            {
                validQuotes.Add(quote);
            }
            else
            {
                rejectedReasons[quote.Symbol + "_" + quote.Source] = validation.Reason ?? "Unknown";
            }
        }
        
        if (validQuotes.Count == 0)
        {
            return new DiscoveredPrice
            {
                Symbol = quoteList[0].Symbol,
                Price = 0,
                PriceValidationStatus = PriceValidationStatus.InvalidPrice,
                RejectionReasons = rejectedReasons,
                SourceCount = quoteList.Count,
                ValidSourceCount = 0,
                DiscoveredAt = DateTime.UtcNow
            };
        }
        
        var prices = validQuotes
            .Select(q => q.Price)
            .OrderBy(p => p)
            .ToList();
        
        decimal median;
        int count = prices.Count;
        
        if (count % 2 == 0)
        {
            median = (prices[count / 2 - 1] + prices[count / 2]) / 2;
        }
        else
        {
            median = prices[count / 2];
        }
        
        var deviation = CalculateMaxDeviation(prices, median);
        
        return new DiscoveredPrice
        {
            Symbol = validQuotes[0].Symbol,
            Price = median,
            MinPrice = prices.Min(),
            MaxPrice = prices.Max(),
            PriceDeviationPercent = deviation,
            Sources = validQuotes.Select(q => q.Source).ToList(),
            PricesBySource = validQuotes.ToDictionary(q => q.Source, q => q.Price),
            BidPrice = validQuotes.Where(q => q.BidPrice.HasValue).Select(q => q.BidPrice!.Value).DefaultIfEmpty(median).Median(),
            AskPrice = validQuotes.Where(q => q.AskPrice.HasValue).Select(q => q.AskPrice!.Value).DefaultIfEmpty(median).Median(),
            Change24h = validQuotes.Where(q => q.Change24h.HasValue).Select(q => q.Change24h!.Value).DefaultIfEmpty(0).Median(),
            Timestamp = validQuotes.Max(q => q.Timestamp),
            PriceValidationStatus = PriceValidationStatus.Valid,
            RejectionReasons = rejectedReasons,
            SourceCount = quoteList.Count,
            ValidSourceCount = validQuotes.Count,
            DiscoveredAt = DateTime.UtcNow
        };
    }
    
    /// <summary>
    /// Tek bir fiyat teklifini doğrular (staleness, geçerlilik kontrolü).
    /// </summary>
    public async Task<PriceValidationResult> ValidateQuoteAsync(
        PriceQuote quote,
        CancellationToken cancellationToken = default)
    {
        if (quote == null)
        {
            return new PriceValidationResult
            {
                IsValid = false,
                Status = PriceValidationStatus.NetworkError,
                Reason = "Quote is null"
            };
        }
        
        if (!string.IsNullOrEmpty(quote.ErrorMessage))
        {
            return new PriceValidationResult
            {
                IsValid = false,
                Status = PriceValidationStatus.SourceError,
                Reason = quote.ErrorMessage
            };
        }
        
        if (quote.Price <= 0)
        {
            return new PriceValidationResult
            {
                IsValid = false,
                Status = PriceValidationStatus.InvalidPrice,
                Reason = $"Invalid price: {quote.Price}"
            };
        }
        
        var now = DateTime.UtcNow;
        var age = now - quote.Timestamp;
        
        if (age.TotalSeconds > _config.MaxStalenessSeconds)
        {
            return new PriceValidationResult
            {
                IsValid = false,
                Status = PriceValidationStatus.Stale,
                Reason = $"Data is stale: {age.TotalSeconds:F1}s old (max: {_config.MaxStalenessSeconds}s)"
            };
        }
        
        if (_config.MaxPriceDeviationPercent > 0 && _config.ReferencePrice > 0)
        {
            var deviationPercent = Math.Abs((quote.Price - _config.ReferencePrice) / _config.ReferencePrice * 100);
            
            if (deviationPercent > _config.MaxPriceDeviationPercent)
            {
                return new PriceValidationResult
                {
                    IsValid = false,
                    Status = PriceValidationStatus.PriceDeviation,
                    Reason = $"Price deviation: {deviationPercent:F2}% (max: {_config.MaxPriceDeviationPercent}%)"
                };
            }
        }
        
        return new PriceValidationResult
        {
            IsValid = true,
            Status = PriceValidationStatus.Valid,
            Reason = null
        };
    }
    
    private decimal CalculateMaxDeviation(List<decimal> prices, decimal median)
    {
        if (prices.Count <= 1 || median == 0)
        {
            return 0;
        }
        
        var maxDeviation = prices.Max(p => Math.Abs((p - median) / median * 100));
        
        return maxDeviation;
    }
}

/// <summary>
/// Keşfedilmiş fiyat bilgisi - Median hesaplama sonucu
/// </summary>
public class DiscoveredPrice
{
    public string Symbol { get; set; } = string.Empty;
    
    public decimal Price { get; set; }
    
    public decimal? MinPrice { get; set; }
    
    public decimal? MaxPrice { get; set; }
    
    public decimal? PriceDeviationPercent { get; set; }
    
    public List<PriceSource> Sources { get; set; } = new();
    
    public Dictionary<PriceSource, decimal> PricesBySource { get; set; } = new();
    
    public decimal? BidPrice { get; set; }
    
    public decimal? AskPrice { get; set; }
    
    public decimal? Change24h { get; set; }
    
    public DateTime Timestamp { get; set; }
    
    public PriceValidationStatus PriceValidationStatus { get; set; }
    
    public Dictionary<string, string> RejectionReasons { get; set; } = new();
    
    public int SourceCount { get; set; }
    
    public int ValidSourceCount { get; set; }
    
    public DateTime DiscoveredAt { get; set; }
}

/// <summary>
/// Fiyat doğrulama sonucu
/// </summary>
public class PriceValidationResult
{
    public bool IsValid { get; set; }
    
    public PriceValidationStatus Status { get; set; }
    
    public string? Reason { get; set; }
}

/// <summary>
/// Median fiyat yapılandırması
/// </summary>
public class MedianPriceConfig
{
    public int MaxStalenessSeconds { get; set; } = 60;
    
    public decimal MaxPriceDeviationPercent { get; set; } = 10;
    
    public decimal ReferencePrice { get; set; } = 0;
    
    public int MinValidSources { get; set; } = 1;
}

/// <summary>
/// Decimal için median extension metodu
/// </summary>
public static class DecimalExtensions
{
    public static decimal Median(this IEnumerable<decimal> source)
    {
        var list = source.OrderBy(x => x).ToList();
        
        if (list.Count == 0)
        {
            return 0;
        }
        
        int count = list.Count;
        
        if (count % 2 == 0)
        {
            return (list[count / 2 - 1] + list[count / 2]) / 2;
        }
        
        return list[count / 2];
    }
}