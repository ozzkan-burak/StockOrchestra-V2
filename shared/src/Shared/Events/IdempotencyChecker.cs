namespace Shared.Events;

using System;
using System.Threading;
using System.Threading.Tasks;

using StackExchange.Redis;

/// <summary>
/// Idempotency Checker - Aynı mesajın tekrar işlenmemesini sağlar.
/// </summary>
public class IdempotencyChecker
{
    private readonly IConnectionMultiplexer _redis;
    private readonly IdempotencyConfig _config;
    
    public IdempotencyChecker(IConnectionMultiplexer redis, IdempotencyConfig config)
    {
        _redis = redis;
        _config = config;
    }
    
    public async Task<bool> IsProcessedAsync(
        string eventId,
        CancellationToken cancellationToken = default)
    {
        var db = _redis.GetDatabase();
        
        var key = GetKey(eventId);
        
        var exists = await db.KeyExistsAsync(key);
        
        return exists;
    }
    
    public async Task<bool> MarkAsProcessedAsync(
        string eventId,
        CancellationToken cancellationToken = default)
    {
        var db = _redis.GetDatabase();
        
        var key = GetKey(eventId);
        
        var added = await db.StringSetAsync(
            key,
            "processed",
            _config.Ttl,
            When.NotExists);
        
        return added;
    }
    
    public async Task<bool> TryProcessAsync(
        string eventId,
        CancellationToken cancellationToken = default)
    {
        var alreadyProcessed = await IsProcessedAsync(eventId, cancellationToken);
        
        if (alreadyProcessed)
        {
            return false;
        }
        
        return await MarkAsProcessedAsync(eventId, cancellationToken);
    }
    
    private string GetKey(string eventId) => $"{_config.KeyPrefix}{eventId}";
}

public class IdempotencyConfig
{
    public string KeyPrefix { get; set; } = "idempotency:";
    
    public TimeSpan Ttl { get; set; } = TimeSpan.FromHours(24);
    
    public bool EnableCleanup { get; set; } = true;
}