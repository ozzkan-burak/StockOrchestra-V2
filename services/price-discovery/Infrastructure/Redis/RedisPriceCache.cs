namespace PriceDiscovery.Infrastructure.Redis;

using System;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

using StackExchange.Redis;

using PriceDiscovery.Application.Interfaces;
using PriceDiscovery.Application.Services;
using PriceDiscovery.Domain.Entities;

/// <summary>
/// Redis fiyat cache'i - Keşfedilen fiyatları Redis'te depolar ve okur.
/// </summary>
public class RedisPriceCache
{
    private readonly IConnectionMultiplexer _redis;
    private readonly RedisCacheConfig _config;
    
    public RedisPriceCache(IConnectionMultiplexer redis, RedisCacheConfig config)
    {
        _redis = redis;
        _config = config;
    }
    
    public async Task SetDiscoveredPriceAsync(
        DiscoveredPrice price,
        CancellationToken cancellationToken = default)
    {
        var db = _redis.GetDatabase();
        
        var key = GetPriceKey(price.Symbol);
        
        var json = JsonSerializer.Serialize(price);
        
        await db.StringSetAsync(key, json, _config.PriceTtl);
    }
    
    public async Task<DiscoveredPrice?> GetDiscoveredPriceAsync(
        string symbol,
        CancellationToken cancellationToken = default)
    {
        var db = _redis.GetDatabase();
        
        var key = GetPriceKey(symbol);
        
        var json = await db.StringGetAsync(key);
        
        if (json.IsNullOrEmpty)
        {
            return null;
        }
        
        return JsonSerializer.Deserialize<DiscoveredPrice>(json!);
    }
    
    public async Task PublishPriceChangeAsync(
        DiscoveredPrice price,
        CancellationToken cancellationToken = default)
    {
        var subscriber = _redis.GetSubscriber();
        
        var channel = RedisChannel.Literal(GetPriceChangeChannel(price.Symbol));
        
        var json = JsonSerializer.Serialize(price);
        
        await subscriber.PublishAsync(channel, json, CommandFlags.FireAndForget);
    }
    
    public async Task SetFetcherHealthAsync(
        PriceSource source,
        FetcherHealthStatus health,
        CancellationToken cancellationToken = default)
    {
        var db = _redis.GetDatabase();
        
        var key = GetFetcherHealthKey(source);
        
        var json = JsonSerializer.Serialize(health);
        
        await db.StringSetAsync(key, json, _config.HealthTtl);
    }
    
    public async Task<FetcherHealthStatus?> GetFetcherHealthAsync(
        PriceSource source,
        CancellationToken cancellationToken = default)
    {
        var db = _redis.GetDatabase();
        
        var key = GetFetcherHealthKey(source);
        
        var json = await db.StringGetAsync(key);
        
        if (json.IsNullOrEmpty)
        {
            return null;
        }
        
        return JsonSerializer.Deserialize<FetcherHealthStatus>(json!);
    }
    
    private static string GetPriceKey(string symbol) => $"price:{symbol.ToUpperInvariant()}:discovered";
    
    private static string GetPriceChangeChannel(string symbol) => $"price:changes:{symbol.ToUpperInvariant()}";
    
    private static string GetFetcherHealthKey(PriceSource source) => $"fetcher:health:{source}";
}

public class RedisCacheConfig
{
    public string ConnectionString { get; set; } = "localhost:6379";
    
    public TimeSpan PriceTtl { get; set; } = TimeSpan.FromMinutes(5);
    
    public TimeSpan HealthTtl { get; set; } = TimeSpan.FromMinutes(1);
    
    public string PriceKeyPrefix { get; set; } = "stockorchestra";
    
    public string HealthKeyPrefix { get; set; } = "stockorchestra:fetcher";
    
    public bool EnablePubSub { get; set; } = true;
}