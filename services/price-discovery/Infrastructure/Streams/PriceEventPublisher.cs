namespace PriceDiscovery.Infrastructure.Streams;

using System;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

using StackExchange.Redis;

using PriceDiscovery.Application.Services;
using Shared.Events;

/// <summary>
/// Redis Streams Publisher - Doğrulanan fiyatları Redis Streams'e gönderir.
/// </summary>
public class PriceEventPublisher
{
    private readonly IConnectionMultiplexer _redis;
    private readonly PublisherConfig _config;
    
    public PriceEventPublisher(IConnectionMultiplexer redis, PublisherConfig config)
    {
        _redis = redis;
        _config = config;
    }
    
    public async Task<string?> PublishPriceUpdatedAsync(
        DiscoveredPrice price,
        CancellationToken cancellationToken = default)
    {
        var db = _redis.GetDatabase();
        
        var streamName = _config.StreamName;
        
        var eventData = new NameValueEntry[]
        {
            new("event_type", nameof(PriceUpdatedEvent)),
            new("event_id", Guid.NewGuid().ToString()),
            new("symbol", price.Symbol),
            new("price", price.Price.ToString()),
            new("bid_price", price.BidPrice?.ToString() ?? ""),
            new("ask_price", price.AskPrice?.ToString() ?? ""),
            new("valid_source_count", price.ValidSourceCount.ToString()),
            new("change_24h", price.Change24h?.ToString() ?? ""),
            new("timestamp", price.Timestamp.ToString("O")),
            new("discovered_at", price.DiscoveredAt.ToString("O")),
            new("published_at", DateTime.UtcNow.ToString("O"))
        };
        
        await db.StreamAddAsync(streamName, eventData);
        
        _config.Logger?.LogInformation(
            "Published price event for {Symbol}: {Price}",
            price.Symbol,
            price.Price);
        
        return streamName;
    }
}

public class PublisherConfig
{
    public string StreamName { get; set; } = "prices:stream";
    
    public int MaxStreamLength { get; set; } = 10000;
    
    public bool EnableTrim { get; set; } = true;
    
    public ILogger? Logger { get; set; }
}

public interface ILogger
{
    void LogInformation(string message, params object[] args);
}