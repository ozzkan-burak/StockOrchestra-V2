namespace PortfolioManager.Infrastructure.Repositories;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using PortfolioManager.Application.Interfaces;
using PortfolioManager.Domain.Entities;
using PortfolioManager.Infrastructure.Data;

using LedgerSide = PortfolioManager.Domain.Entities.LedgerSide;
using LedgerStatus = PortfolioManager.Domain.Entities.LedgerStatus;
using LedgerTransactionType = PortfolioManager.Domain.Entities.LedgerTransactionType;

public class LedgerRepository : ILedgerRepository
{
    private readonly PortfolioDbContext _context;
    
    public LedgerRepository(PortfolioDbContext context)
    {
        _context = context;
    }
    
    public async Task<AssetLedger> AddLedgerEntryAsync(
        Guid userId,
        Guid? assetId,
        LedgerTransactionType transactionType,
        LedgerSide side,
        decimal quantity,
        decimal? priceAtTime,
        decimal? totalNotional,
        decimal commissionAmount = 0,
        string? externalRef = null,
        string? description = null,
        CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        
        decimal? quoteBalanceAfter = null;
        decimal? assetBalanceAfter = null;
        
        if (assetId.HasValue)
        {
            assetBalanceAfter = await CalculateNewAssetBalanceAsync(
                userId, assetId.Value, side, quantity, cancellationToken);
        }
        else
        {
            quoteBalanceAfter = await CalculateNewQuoteBalanceAsync(
                userId, side, totalNotional ?? 0, commissionAmount, cancellationToken);
        }
        
        var ledgerEntry = new AssetLedger
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            AssetId = assetId,
            TransactionType = transactionType,
            Side = side,
            Quantity = quantity,
            PriceAtTime = priceAtTime,
            TotalNotional = totalNotional,
            QuoteBalanceAfter = quoteBalanceAfter,
            AssetBalanceAfter = assetBalanceAfter,
            CommissionAmount = commissionAmount,
            CommissionPaid = true,
            ExternalRef = externalRef,
            Description = description,
            Status = LedgerStatus.Completed,
            CreatedAt = now,
            CompletedAt = now,
            UpdatedAt = now
        };
        
        _context.AssetLedgers.Add(ledgerEntry);
        await _context.SaveChangesAsync(cancellationToken);
        
        return ledgerEntry;
    }
    
    public async Task<decimal> GetAssetBalanceAsync(
        Guid userId,
        Guid assetId,
        CancellationToken cancellationToken = default)
    {
        var entries = await _context.AssetLedgers
            .Where(l => l.UserId == userId 
                && l.AssetId == assetId 
                && l.Status == LedgerStatus.Completed)
            .ToListAsync(cancellationToken);
        
        decimal balance = 0;
        foreach (var entry in entries)
        {
            if (entry.Side == LedgerSide.Debit)
                balance += entry.Quantity;
            else
                balance -= entry.Quantity;
        }
        
        return balance;
    }
    
    public async Task<decimal> GetQuoteBalanceAsync(
        Guid userId,
        string quoteCurrency = "USD",
        CancellationToken cancellationToken = default)
    {
        var entries = await _context.AssetLedgers
            .Where(l => l.UserId == userId 
                && (l.AssetId == null)
                && l.Status == LedgerStatus.Completed)
            .ToListAsync(cancellationToken);
        
        decimal balance = 0;
        foreach (var entry in entries)
        {
            var notional = entry.TotalNotional ?? 0;
            var commission = entry.CommissionAmount;
            
            switch (entry.TransactionType)
            {
                case LedgerTransactionType.Deposit:
                case LedgerTransactionType.Sell:
                    balance += notional - commission;
                    break;
                case LedgerTransactionType.Withdrawal:
                case LedgerTransactionType.Buy:
                case LedgerTransactionType.Fee:
                    balance -= (notional + commission);
                    break;
            }
        }
        
        return balance;
    }
    
    public async Task<IEnumerable<AssetLedger>> GetUserLedgerEntriesAsync(
        Guid userId,
        int page = 1,
        int pageSize = 50,
        CancellationToken cancellationToken = default)
    {
        return await _context.AssetLedgers
            .Where(l => l.UserId == userId)
            .OrderByDescending(l => l.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);
    }
    
    public async Task<IEnumerable<AssetLedger>> GetAssetLedgerEntriesAsync(
        Guid userId,
        Guid assetId,
        int page = 1,
        int pageSize = 50,
        CancellationToken cancellationToken = default)
    {
        return await _context.AssetLedgers
            .Where(l => l.UserId == userId && l.AssetId == assetId)
            .OrderByDescending(l => l.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);
    }
    
    public async Task<decimal?> GetLastPriceAsync(
        Guid userId,
        Guid assetId,
        CancellationToken cancellationToken = default)
    {
        var lastEntry = await _context.AssetLedgers
            .Where(l => l.UserId == userId 
                && l.AssetId == assetId 
                && l.Status == LedgerStatus.Completed)
            .OrderByDescending(l => l.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);
        
        return lastEntry?.PriceAtTime;
    }
    
    private async Task<decimal> CalculateNewAssetBalanceAsync(
        Guid userId,
        Guid assetId,
        LedgerSide side,
        decimal quantity,
        CancellationToken cancellationToken)
    {
        var currentBalance = await GetAssetBalanceAsync(userId, assetId, cancellationToken);
        
        if (side == LedgerSide.Debit)
            return currentBalance + quantity;
        else
            return currentBalance - quantity;
    }
    
    private async Task<decimal> CalculateNewQuoteBalanceAsync(
        Guid userId,
        LedgerSide side,
        decimal totalNotional,
        decimal commissionAmount,
        CancellationToken cancellationToken)
    {
        var currentBalance = await GetQuoteBalanceAsync(userId, cancellationToken: cancellationToken);
        
        if (side == LedgerSide.Debit)
            return currentBalance + totalNotional - commissionAmount;
        else
            return currentBalance - totalNotional - commissionAmount;
    }
}