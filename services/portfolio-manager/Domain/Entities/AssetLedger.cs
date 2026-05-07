namespace PortfolioManager.Domain.Entities;

using System;

public class AssetLedger
{
    public Guid Id { get; set; }
    
    public Guid UserId { get; set; }
    
    public Guid? AssetId { get; set; }
    
    public LedgerTransactionType TransactionType { get; set; }
    
    public LedgerSide Side { get; set; }
    
    public decimal Quantity { get; set; }
    
    public decimal? PriceAtTime { get; set; }
    
    public decimal? TotalNotional { get; set; }
    
    public decimal? QuoteBalanceAfter { get; set; }
    
    public decimal? AssetBalanceAfter { get; set; }
    
    public decimal CommissionAmount { get; set; }
    
    public bool CommissionPaid { get; set; }
    
    public string? ExternalRef { get; set; }
    
    public string? Description { get; set; }
    
    public LedgerStatus Status { get; set; }
    
    public DateTime CreatedAt { get; set; }
    
    public DateTime? CompletedAt { get; set; }
    
    public DateTime UpdatedAt { get; set; }
    
    public User? User { get; set; }
    
    public Asset? Asset { get; set; }
}