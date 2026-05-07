namespace AnalyticalStore.Domain.Entities;

using System;

/// <summary>
/// Fiyat entitysi - TimescaleDB'de saklanan fiyat verisi.
/// </summary>
public class PriceRecord
{
    public long Id { get; set; }
    
    public DateTime CreatedAt { get; set; }
    
    public string Symbol { get; set; } = string.Empty;
    
    public Guid? AssetId { get; set; }
    
    public decimal CurrentPrice { get; set; }
    
    public decimal? BidPrice { get; set; }
    
    public decimal? AskPrice { get; set; }
    
    public decimal? Change24h { get; set; }
    
    public string? Sources { get; set; }
    
    public int ValidSourceCount { get; set; }
    
    public DateTime? SourceTimestamp { get; set; }
    
    public string ValidationStatus { get; set; } = "valid";
}