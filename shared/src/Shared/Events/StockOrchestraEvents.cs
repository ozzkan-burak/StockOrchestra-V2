namespace Shared.Events;

using System;

/// <summary>
/// StockOrchestra sisteminin olay modelleri (Event Models).
/// Tüm servisler arasında paylaşılan "alfabe" - ortak dil.
/// </summary>
/// <remarks>
/// Mimari Mantık:
/// - Record yapıları: Değiştirilemez (immutable) veri taşıyıcıları
/// - Versioning: Olayların versiyonlanması
/// - Serialization: JSON ile serialize edilebilir
/// </remarks>
public static class EventTypes
{
    public const string PriceUpdated = "price.updated";
    public const string TransactionCreated = "transaction.created";
    public const string PortfolioValueCalculated = "portfolio.value.calculated";
    public const string AssetBalanceChanged = "asset.balance.changed";
}

/// <summary>
/// Fiyat güncelleme olayı - Price-Discovery tarafından yayınlanır.
/// </summary>
public record PriceUpdatedEvent
{
    /// <summary>
    /// Olay tipi
    /// </summary>
    public string EventType { get; init; } = EventTypes.PriceUpdated;
    
    /// <summary>
    /// Olay versiyonu
    /// </summary>
    public string Version { get; init; } = "1.0";
    
    /// <summary>
    /// Benzersiz olay ID'si (idempotency için)
    /// </summary>
    public string EventId { get; init; } = Guid.NewGuid().ToString();
    
    /// <summary>
    /// Varlık sembolü
    /// </summary>
    public string Symbol { get; init; } = string.Empty;
    
    /// <summary>
    /// Varlık ID'si (veritabanı)
    /// </summary>
    public Guid? AssetId { get; init; }
    
    /// <summary>
    /// Keşfedilen fiyat (median)
    /// </summary>
    public decimal Price { get; init; }
    
    /// <summary>
    /// Bid fiyatı
    /// </summary>
    public decimal? BidPrice { get; init; }
    
    /// <summary>
    /// Ask fiyatı
    /// </summary>
    public decimal? AskPrice { get; init; }
    
    /// <summary>
    /// Fiyat kaynağı (multi-source)
    /// </summary>
    public List<string> Sources { get; init; } = new();
    
    /// <summary>
    /// Geçerli kaynak sayısı
    /// </summary>
    public int ValidSourceCount { get; init; }
    
    /// <summary>
    /// 24 saatlik değişim
    /// </summary>
    public decimal? Change24h { get; init; }
    
    /// <summary>
    /// Fiyat zamanı
    /// </summary>
    public DateTime Timestamp { get; init; }
    
    /// <summary>
    /// Keşfedilme zamanı
    /// </summary>
    public DateTime DiscoveredAt { get; init; }
}

/// <summary>
/// İşlem oluşturma olayı - Kullanıcı işlem yaptığında yayınlanır.
/// </summary>
public record TransactionCreatedEvent
{
    public string EventType { get; init; } = EventTypes.TransactionCreated;
    
    public string Version { get; init; } = "1.0";
    
    public string EventId { get; init; } = Guid.NewGuid().ToString();
    
    /// <summary>Kullanıcı ID'si</summary>
    public Guid UserId { get; init; }
    
    /// <summary>Varlık ID'si</summary>
    public Guid? AssetId { get; init; }
    
    /// <summary>Varlık sembolü</summary>
    public string Symbol { get; init; } = string.Empty;
    
    /// <summary>İşlem tipi (BUY, SELL, vb.)</summary>
    public string TransactionType { get; init; } = string.Empty;
    
    /// <summary>İşlem miktarı</summary>
    public decimal Quantity { get; init; }
    
    /// <summary>Birim fiyatı</summary>
    public decimal? PriceAtTime { get; init; }
    
    /// <summary>Toplam tutar</summary>
    public decimal? TotalNotional { get; init; }
    
    /// <summary>Komisyon</summary>
    public decimal CommissionAmount { get; init; }
    
    /// <summary>Harici referans</summary>
    public string? ExternalRef { get; init; }
    
    /// <summary>Oluşturulma zamanı</summary>
    public DateTime CreatedAt { get; init; }
}

/// <summary>
/// Portföy değeri hesaplama olayı - Portfolio-Manager tarafından yayınlanır.
/// </summary>
public record PortfolioValueCalculatedEvent
{
    public string EventType { get; init; } = EventTypes.PortfolioValueCalculated;
    
    public string Version { get; init; } = "1.0";
    
    public string EventId { get; init; } = Guid.NewGuid().ToString();
    
    public Guid UserId { get; init; }
    
    /// <summary>Varlık ID'si</summary>
    public Guid? AssetId { get; init; }
    
    public string Symbol { get; init; } = string.Empty;
    
    /// <summary>Mevcut miktar</summary>
    public decimal Quantity { get; init; }
    
    /// <summary>Güncel fiyat</summary>
    public decimal CurrentPrice { get; init; }
    
    /// <summary>Toplam değer (Quantity * Price)</summary>
    public decimal TotalValue { get; init; }
    
    /// <summary>24s değişim</summary>
    public decimal? Change24h { get; init; }
    
    /// <summary>Hesaplanma zamanı</summary>
    public DateTime CalculatedAt { get; init; }
}

/// <summary>
/// Varlık bakiyesi değişikliği olayı.
/// </summary>
public record AssetBalanceChangedEvent
{
    public string EventType { get; init; } = EventTypes.AssetBalanceChanged;
    
    public string Version { get; init; } = "1.0";
    
    public string EventId { get; init; } = Guid.NewGuid().ToString();
    
    public Guid UserId { get; init; }
    
    public Guid AssetId { get; init; }
    
    public string Symbol { get; init; } = string.Empty;
    
    /// <summary>Önceki bakiye</summary>
    public decimal PreviousBalance { get; init; }
    
    /// <summary>Yeni bakiye</summary>
    public decimal NewBalance { get; init; }
    
    /// <summary>Değişim miktarı</summary>
    public decimal ChangeAmount { get; init; }
    
    /// <summary>İşlem tipi</summary>
    public string TransactionType { get; init; } = string.Empty;
    
    public DateTime ChangedAt { get; init; }
}