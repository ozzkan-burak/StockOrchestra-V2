namespace PortfolioManager.Application.Interfaces;

using System;
using System.Threading;
using System.Threading.Tasks;
using PortfolioManager.Domain.Entities;

using LedgerSide = PortfolioManager.Domain.Entities.LedgerSide;
using LedgerTransactionType = PortfolioManager.Domain.Entities.LedgerTransactionType;

/// <summary>
/// Ledger Repository Arayüzü - Append-only veri erişim katmanı.
/// Her işlem yeni kayıt olarak eklenir, güncelleme yapılmaz.
/// </summary>
public interface ILedgerRepository
{
    /// <summary>
    /// Yeni bir ledger kaydı ekler (Append-only).
    /// </summary>
    Task<AssetLedger> AddLedgerEntryAsync(
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
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Kullanıcının belirli bir varlıktaki bakiyesini hesaplar.
    /// </summary>
    Task<decimal> GetAssetBalanceAsync(
        Guid userId,
        Guid assetId,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Kullanıcının quote currency bakiyesini hesaplar.
    /// </summary>
    Task<decimal> GetQuoteBalanceAsync(
        Guid userId,
        string quoteCurrency = "USD",
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Kullanıcının tüm ledger kayıtlarını getirir.
    /// </summary>
    Task<IEnumerable<AssetLedger>> GetUserLedgerEntriesAsync(
        Guid userId,
        int page = 1,
        int pageSize = 50,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Kullanıcının belirli bir varlıktaki işlem geçmişini getirir.
    /// </summary>
    Task<IEnumerable<AssetLedger>> GetAssetLedgerEntriesAsync(
        Guid userId,
        Guid assetId,
        int page = 1,
        int pageSize = 50,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Belirli bir kullanıcı ve varlık için son işlem fiyatını getirir.
    /// </summary>
    Task<decimal?> GetLastPriceAsync(
        Guid userId,
        Guid assetId,
        CancellationToken cancellationToken = default);
}