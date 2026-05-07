namespace PriceDiscovery.Application.Interfaces;

using System;
using System.Threading;
using System.Threading.Tasks;

using PriceSource = PriceDiscovery.Domain.Entities.PriceSource;

/// <summary>
/// Fiyat verisi sağlayıcıfetcher arayüzü.
/// Her kaynak (Binance, Yahoo Finance, CoinGecko vb.) bu arayüzü implement eder.
/// </summary>
/// <remarks>
/// Mimari Mantık:
/// - Async/Await: Tüm işlemler asenkron, hiçbir zaman blocking kod çalışmaz
/// - CancellationToken: Uzun süren işlemler iptal edilebilir
/// - Fault Tolerance: Bir kaynak hatası tüm sistemi durdurmaz
/// </remarks>
public interface IPriceFetcher
{
    /// <summary>
    /// Fetcher'ın desteklediği varlık türleri (crypto, stock, forex, commodity)
    /// </summary>
    PriceSource Source { get; }
    
    /// <summary>
    /// Bu fetcher'ın aktif olup olmadığı (Circuit Breaker tarafından yönetilir)
    /// </summary>
    bool IsEnabled { get; set; }
    
    /// <summary>
    /// Belirli bir varlık için güncel fiyatı asenkron olarak çeker.
    /// </summary>
    /// <param name="symbol">Varlık sembolü (örn: BTC, ETH, AAPL)</param>
    /// <param name="cancellationToken">İptal belirteci</param>
    /// <returns>Fiyat bilgisi veya null</returns>
    Task<PriceQuote?> FetchPriceAsync(
        string symbol,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Birden fazla varlık için toplu fiyat çeker.
    /// </summary>
    /// <param name="symbols">Varlık sembolleri dizisi</param>
    /// <param name="cancellationToken">İptal belirteci</param>
    /// <returns>Fiyat bilgileri sözlüğü</returns>
    Task<IDictionary<string, PriceQuote>> FetchPricesAsync(
        string[] symbols,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Fetcher'ın sağlık durumunu kontrol eder.
    /// </summary>
    /// <param name="cancellationToken">İptal belirteci</param>
    /// <returns>Sağlık durumu</returns>
    Task<FetcherHealthStatus> CheckHealthAsync(
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Fiyat teklifi - Tek bir kaynaktan gelen fiyat bilgisi
/// </summary>
public class PriceQuote
{
    /// <summary>
    /// Varlık sembolü
    /// </summary>
    public string Symbol { get; set; } = string.Empty;
    
    /// <summary>
    /// Güncel fiyat
    /// </summary>
    public decimal Price { get; set; }
    
    /// <summary>
    /// Alış fiyatı (Bid)
    /// </summary>
    public decimal? BidPrice { get; set; }
    
    /// <summary>
    /// Satış fiyatı (Ask)
    /// </summary>
    public decimal? AskPrice { get; set; }
    
    /// <summary>
    /// 24 saatlik değişim yüzdesi
    /// </summary>
    public decimal? Change24h { get; set; }
    
    /// <summary>
    /// 24 saatlik en yüksek fiyat
    /// </summary>
    public decimal? High24h { get; set; }
    
    /// <summary>
    /// 24 saatlik en düşük fiyat
    /// </summary>
    public decimal? Low24h { get; set; }
    
    /// <summary>
    /// 24 saatlik işlem hacmi
    /// </summary>
    public decimal? Volume24h { get; set; }
    
    /// <summary>
    /// Fiyat zamanı (kaynağın verdiği zaman damgası)
    /// </summary>
    public DateTime Timestamp { get; set; }
    
    /// <summary>
    /// Fiyatın alındığı kaynak
    /// </summary>
    public PriceSource Source { get; set; }
    
    /// <summary>
    /// Verinin ham yanıtı (hata durumunda debug için)
    /// </summary>
    public string? RawResponse { get; set; }
    
    /// <summary>
    /// Hata mesajı (varsa)
    /// </summary>
    public string? ErrorMessage { get; set; }
    
    /// <summary>
    /// Fiyatın geçerli olup olmadığı kontrolü
    /// </summary>
    public bool IsValid => !string.IsNullOrEmpty(Symbol) && Price > 0 && ErrorMessage == null;
}

/// <summary>
/// Fetcher sağlık durumu
/// </summary>
public class FetcherHealthStatus
{
    public bool IsHealthy { get; set; }
    
    public TimeSpan? Latency { get; set; }
    
    public string? ErrorMessage { get; set; }
    
    public DateTime CheckedAt { get; set; }
}