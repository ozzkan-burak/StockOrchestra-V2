namespace PortfolioManager.Domain.Entities;

using System;
using System.Numerics;

/// <summary>
/// Varlık entitysi - Sistemde takip edilen finansal varlıkları temsil eder.
/// Örnek: BTC, ETH, AAPL, GOOGL vb.
/// </summary>
public class Asset
{
    public Guid Id { get; set; }
    
    public string Symbol { get; set; } = string.Empty;
    
    public string Name { get; set; } = string.Empty;
    
    public string AssetType { get; set; } = string.Empty;
    
    public string QuoteCurrency { get; set; } = "USD";
    
    public decimal MinQuantity { get; set; }
    
    public decimal MinNotional { get; set; }
    
    public bool IsActive { get; set; }
    
    public int DecimalPlaces { get; set; }
    
    public decimal CommissionRate { get; set; }
    
    public DateTime? LastTradedAt { get; set; }
    
    public DateTime CreatedAt { get; set; }
    
    public DateTime UpdatedAt { get; set; }
}