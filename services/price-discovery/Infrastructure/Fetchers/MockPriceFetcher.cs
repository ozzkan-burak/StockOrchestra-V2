namespace PriceDiscovery.Infrastructure.Fetchers;

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

using PriceDiscovery.Application.Interfaces;
using PriceDiscovery.Domain.Entities;

public class MockPriceFetcher : IPriceFetcher
{
    private readonly Random _random = new();
    private readonly Dictionary<string, decimal> _basePrices = new()
    {
        { "BTC", 67000m },
        { "ETH", 3500m },
        { "AAPL", 175m },
        { "GOOGL", 140m },
        { "TSLA", 180m },
        { "XAUUSD", 2350m }
    };

    public PriceSource Source => PriceSource.Mock;
    public bool IsEnabled { get; set; } = true;

    public async Task<PriceQuote?> FetchPriceAsync(
        string symbol,
        CancellationToken cancellationToken = default)
    {
        await Task.Delay(50, cancellationToken);

        if (!_basePrices.TryGetValue(symbol.ToUpperInvariant(), out var basePrice))
        {
            basePrice = 100m;
        }

        var variation = (decimal)((_random.NextDouble() - 0.5) * 0.02);
        var price = basePrice * (1 + variation);

        return new PriceQuote
        {
            Symbol = symbol.ToUpperInvariant(),
            Price = Math.Round(price, 2),
            BidPrice = Math.Round(price * 0.9995m, 2),
            AskPrice = Math.Round(price * 1.0005m, 2),
            Timestamp = DateTime.UtcNow,
            Source = Source
        };
    }

    public async Task<IDictionary<string, PriceQuote>> FetchPricesAsync(
        string[] symbols,
        CancellationToken cancellationToken = default)
    {
        var result = new Dictionary<string, PriceQuote>();
        
        foreach (var symbol in symbols)
        {
            var quote = await FetchPriceAsync(symbol, cancellationToken);
            if (quote != null)
            {
                result[symbol] = quote;
            }
        }
        
        return result;
    }

    public Task<FetcherHealthStatus> CheckHealthAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult(new FetcherHealthStatus
        {
            IsHealthy = true,
            CheckedAt = DateTime.UtcNow
        });
    }
}