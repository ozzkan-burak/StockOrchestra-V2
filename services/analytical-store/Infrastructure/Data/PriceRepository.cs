namespace AnalyticalStore.Infrastructure.Data;

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

using Dapper;

using AnalyticalStore.Domain.Entities;

/// <summary>
/// Dapper Repository - TimescaleDB'ye fiyat verisi kaydetmek için kullanılır.
/// </summary>
public class PriceRepository
{
    private readonly string _connectionString;
    
    public PriceRepository(string connectionString)
    {
        _connectionString = connectionString;
    }
    
    public async Task<int> InsertPriceAsync(
        PriceRecord price,
        CancellationToken cancellationToken = default)
    {
        const string sql = @"
            INSERT INTO prices (
                created_at,
                symbol,
                asset_id,
                price,
                bid_price,
                ask_price,
                change_24h,
                sources,
                valid_source_count,
                source_timestamp,
                validation_status
            ) VALUES (
                @CreatedAt,
                @Symbol,
                @AssetId,
                @CurrentPrice,
                @BidPrice,
                @AskPrice,
                @Change24h,
                @Sources::jsonb,
                @ValidSourceCount,
                @SourceTimestamp,
                @ValidationStatus
            )";
        
        using var connection = new Npgsql.NpgsqlConnection(_connectionString);
        
        await connection.OpenAsync(cancellationToken);
        
        return await connection.ExecuteAsync(
            sql,
            new
            {
                price.CreatedAt,
                price.Symbol,
                price.AssetId,
                price.CurrentPrice,
                price.BidPrice,
                price.AskPrice,
                price.Change24h,
                price.Sources,
                price.ValidSourceCount,
                price.SourceTimestamp,
                price.ValidationStatus
            },
            commandTimeout: 30);
    }
    
    public async Task<int> InsertPricesBulkAsync(
        IEnumerable<PriceRecord> prices,
        CancellationToken cancellationToken = default)
    {
        const string sql = @"
            INSERT INTO prices (
                created_at,
                symbol,
                asset_id,
                price,
                bid_price,
                ask_price,
                change_24h,
                sources,
                valid_source_count,
                source_timestamp,
                validation_status
            ) VALUES (
                @CreatedAt,
                @Symbol,
                @AssetId,
                @CurrentPrice,
                @BidPrice,
                @AskPrice,
                @Change24h,
                @Sources::jsonb,
                @ValidSourceCount,
                @SourceTimestamp,
                @ValidationStatus
            )";
        
        using var connection = new Npgsql.NpgsqlConnection(_connectionString);
        
        await connection.OpenAsync(cancellationToken);
        
        return await connection.ExecuteAsync(
            sql,
            prices,
            commandTimeout: 60);
    }
    
    public async Task<IEnumerable<PriceRecord>> GetRecentPricesAsync(
        string symbol,
        int count = 100,
        CancellationToken cancellationToken = default)
    {
        const string sql = @"
            SELECT 
                id,
                created_at AS CreatedAt,
                symbol AS Symbol,
                asset_id AS AssetId,
                price AS CurrentPrice,
                bid_price AS BidPrice,
                ask_price AS AskPrice,
                change_24h AS Change24h,
                sources AS Sources,
                valid_source_count AS ValidSourceCount,
                source_timestamp AS SourceTimestamp,
                validation_status AS ValidationStatus
            FROM prices
            WHERE symbol = @Symbol
            ORDER BY created_at DESC
            LIMIT @Count";
        
        using var connection = new Npgsql.NpgsqlConnection(_connectionString);
        
        await connection.OpenAsync(cancellationToken);
        
        return await connection.QueryAsync<PriceRecord>(
            sql,
            new { Symbol = symbol, Count = count },
            commandTimeout: 30);
    }
    
    public async Task<IEnumerable<OhlcResult>> Get1MinuteOhlcAsync(
        string symbol,
        DateTime from,
        DateTime to,
        CancellationToken cancellationToken = default)
    {
        const string sql = @"
            SELECT
                bucket,
                symbol,
                open,
                high,
                low,
                close,
                avg,
                volume,
                change,
                change_percent
            FROM prices_1m_ohlc
            WHERE symbol = @Symbol
              AND bucket >= @From
              AND bucket <= @To
            ORDER BY bucket DESC";
        
        using var connection = new Npgsql.NpgsqlConnection(_connectionString);
        
        await connection.OpenAsync(cancellationToken);
        
        return await connection.QueryAsync<OhlcResult>(
            sql,
            new { Symbol = symbol, From = from, To = to },
            commandTimeout: 30);
    }
    
    public async Task<IEnumerable<OhlcResult>> Get1HourOhlcAsync(
        string symbol,
        DateTime from,
        DateTime to,
        CancellationToken cancellationToken = default)
    {
        const string sql = @"
            SELECT
                bucket,
                symbol,
                open,
                high,
                low,
                close,
                avg,
                volume,
                change,
                change_percent
            FROM prices_1h_ohlc
            WHERE symbol = @Symbol
              AND bucket >= @From
              AND bucket <= @To
            ORDER BY bucket DESC";
        
        using var connection = new Npgsql.NpgsqlConnection(_connectionString);
        
        await connection.OpenAsync(cancellationToken);
        
        return await connection.QueryAsync<OhlcResult>(
            sql,
            new { Symbol = symbol, From = from, To = to },
            commandTimeout: 30);
    }
    
    public async Task<IEnumerable<OhlcResult>> Get1DayOhlcAsync(
        string symbol,
        DateTime from,
        DateTime to,
        CancellationToken cancellationToken = default)
    {
        const string sql = @"
            SELECT
                bucket,
                symbol,
                open,
                high,
                low,
                close,
                avg,
                volume,
                change,
                change_percent
            FROM prices_1d_ohlc
            WHERE symbol = @Symbol
              AND bucket >= @From
              AND bucket <= @To
            ORDER BY bucket DESC";
        
        using var connection = new Npgsql.NpgsqlConnection(_connectionString);
        
        await connection.OpenAsync(cancellationToken);
        
        return await connection.QueryAsync<OhlcResult>(
            sql,
            new { Symbol = symbol, From = from, To = to },
            commandTimeout: 30);
    }
}

public class OhlcResult
{
    public DateTime Bucket { get; set; }
    
    public string Symbol { get; set; } = string.Empty;
    
    public decimal Open { get; set; }
    
    public decimal High { get; set; }
    
    public decimal Low { get; set; }
    
    public decimal Close { get; set; }
    
    public decimal Avg { get; set; }
    
    public long Volume { get; set; }
    
    public decimal Change { get; set; }
    
    public decimal ChangePercent { get; set; }
}