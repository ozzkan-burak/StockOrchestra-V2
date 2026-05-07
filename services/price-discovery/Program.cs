using PriceDiscovery;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

/// <summary>
/// Price Discovery Service Konfigürasyonu ve DI Container kurulumu.
/// </summary>
/// <remarks>
/// Mimari Mantık:
/// - Host.CreateApplicationBuilder: Worker service için hazır hosting
/// - HttpClient Factory: HttpClient yaşam döngüsünü yönetir
/// - Logging: Yapılandırılabilir loglama
/// - Configuration: appsettings.json'dan yükleme
/// </remarks>
var builder = Host.CreateApplicationBuilder(args);

// Worker config - register directly
builder.Services.AddSingleton(new WorkerConfig
{
    PollingIntervalMs = 5000,
    SymbolsToMonitor = new List<string> { "BTC", "ETH", "AAPL", "GOOGL", "TSLA", "XAUUSD" },
    RedisConnectionString = "localhost:6379,password=redis_secure_pass_2024,abortConnect=false",
    EnablePubSub = true,
    UseMockData = true
});

// HttpClient Factory ekle - her fetcher için ayrı instance
builder.Services.AddHttpClient("Binance", client =>
{
    client.Timeout = TimeSpan.FromSeconds(10);
});
builder.Services.AddHttpClient("YahooFinance", client =>
{
    client.Timeout = TimeSpan.FromSeconds(10);
});

// Worker servisini kaydet
builder.Services.AddHostedService<Worker>();

// Logging yapılandırması
builder.Logging.AddConsole();

var host = builder.Build();

host.Run();