namespace Shared.Events;

using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

using StackExchange.Redis;

/// <summary>
/// Redis Streams Producer - Olayları Redis Streams'e basan sınıf.
/// </summary>
public class RedisStreamsProducer
{
    private readonly IConnectionMultiplexer _redis;
    private readonly RedisStreamsConfig _config;
    
    public RedisStreamsProducer(IConnectionMultiplexer redis, RedisStreamsConfig config)
    {
        _redis = redis;
        _config = config;
    }
    
    public async Task<string?> PublishEventAsync<T>(
        T evt,
        CancellationToken cancellationToken = default) where T : class
    {
        var db = _redis.GetDatabase();
        
        var streamName = GetStreamNameForEvent(evt);
        
        var eventData = new NameValueEntry[]
        {
            new("event_type", evt.GetType().Name),
            new("event_data", JsonSerializer.Serialize(evt)),
            new("published_at", DateTime.UtcNow.ToString("O"))
        };
        
        var messageId = await db.StreamAddAsync(streamName, eventData);
        
        return messageId.ToString();
    }
    
    private string GetStreamNameForEvent<T>(T evt)
    {
        var eventTypeName = evt?.GetType().Name ?? typeof(T).Name;
        
        return eventTypeName switch
        {
            nameof(PriceUpdatedEvent) => _config.PriceStreamName,
            nameof(TransactionCreatedEvent) => _config.TransactionStreamName,
            nameof(PortfolioValueCalculatedEvent) => _config.PortfolioValueStreamName,
            nameof(AssetBalanceChangedEvent) => _config.AssetBalanceStreamName,
            _ => $"custom:{eventTypeName}"
        };
    }
}

/// <summary>
/// Redis Streams Consumer - Olayları Redis Streams'ten tüketen sınıf.
/// </summary>
public class RedisStreamsConsumer
{
    private readonly IConnectionMultiplexer _redis;
    private readonly RedisStreamsConfig _config;
    
    public RedisStreamsConsumer(IConnectionMultiplexer redis, RedisStreamsConfig config)
    {
        _redis = redis;
        _config = config;
    }
    
    public async Task CreateConsumerGroupAsync(
        string streamName,
        string groupName,
        CancellationToken cancellationToken = default)
    {
        var db = _redis.GetDatabase();
        
        try
        {
            await db.StreamCreateConsumerGroupAsync(streamName, groupName);
        }
        catch (RedisException)
        {
        }
    }
    
    public async Task<List<StreamEvent>> ConsumeEventsAsync(
        string streamName,
        string groupName,
        string consumerName,
        int count = 10,
        bool acknowledge = true,
        CancellationToken cancellationToken = default)
    {
        var db = _redis.GetDatabase();
        
        var events = new List<StreamEvent>();
        
        var entries = await db.StreamReadGroupAsync(
            streamName,
            groupName,
            consumerName,
            count: count);
        
        foreach (var entry in entries)
        {
            var eventData = new Dictionary<string, string>();
            
            foreach (var item in entry.Values)
            {
                eventData[item.Name.ToString()] = item.Value.ToString();
            }
            
            events.Add(new StreamEvent
            {
                MessageId = entry.Id.ToString(),
                EventType = eventData.GetValueOrDefault("event_type") ?? "",
                EventData = eventData.GetValueOrDefault("event_data") ?? "",
                ReceivedAt = DateTime.UtcNow
            });
        }
        
        if (acknowledge && events.Count > 0)
        {
            var messageIds = entries.Select(e => e.Id).ToArray();
            await db.StreamAcknowledgeAsync(streamName, groupName, messageIds);
        }
        
        return events;
    }
}

/// <summary>
/// Tüketilen stream olayı
/// </summary>
public class StreamEvent
{
    public string MessageId { get; set; } = string.Empty;
    
    public string EventType { get; set; } = string.Empty;
    
    public string EventData { get; set; } = string.Empty;
    
    public DateTime ReceivedAt { get; set; }
}

/// <summary>
/// Redis Streams yapılandırması
/// </summary>
public class RedisStreamsConfig
{
    public string ConnectionString { get; set; } = "localhost:6379";
    
    public string PriceStreamName { get; set; } = "prices:stream";
    
    public string TransactionStreamName { get; set; } = "transactions:stream";
    
    public string PortfolioValueStreamName { get; set; } = "portfolio:values:stream";
    
    public string AssetBalanceStreamName { get; set; } = "asset:balances:stream";
    
    public string DefaultConsumerGroup { get; set; } = "stockorchestra-consumers";
    
    public int MaxStreamLength { get; set; } = 10000;
    
    public bool EnableTrim { get; set; } = true;
}