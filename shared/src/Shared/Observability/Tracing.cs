namespace Shared.Observability;

using System;
using System.Diagnostics;

/// <summary>
/// Distributed Tracing Service - OpenTelemetry entegrasyonu için kullanılır.
/// </summary>
/// <remarks>
/// Mimari Mantık:
/// - Trace ID: İsteğin tüm sistem boyunca takip edilmesini sağlar
/// - Span ID: Tek bir mikroservis içindeki işlemi temsil eder
/// - Parent-Child: İstek zincirinde bağlantıyı korur
/// - Baggage: İstek boyunca taşınan özel veriler (user ID, vb.)
/// </remarks>
public static class Tracing
{
    public const string ActivitySourceName = "StockOrchestra";
    
    private static readonly ActivitySource _source = new(ActivitySourceName, "1.0.0");
    
    /// <summary>
    /// Yeni bir Activity (Span) başlatır.
    /// </summary>
    public static Activity? StartSpan(string spanName, Activity? parent = null)
    {
        var activityKind = parent != null ? ActivityKind.Internal : ActivityKind.Internal;
        
        var tags = new ActivityTagsCollection
        {
            { "service.name", ActivitySourceName },
            { "span.kind", activityKind.ToString() }
        };
        
        return _source.StartActivity(
            spanName,
            activityKind,
            parent?.Context ?? default,
            tags: tags);
    }
    
    /// <summary>
    /// Mevcut Activity'yi alır.
    /// </summary>
    public static Activity? Current => Activity.Current;
    
    /// <summary>
    /// Trace ID'yi string olarak döndürür.
    /// </summary>
    public static string? GetTraceId()
    {
        return Activity.Current?.Context.TraceId.ToHexString();
    }
    
    /// <summary>
    /// Span ID'yi string olarak döndürür.
    /// </summary>
    public static string? GetSpanId()
    {
        return Activity.Current?.Context.SpanId.ToHexString();
    }
    
    /// <summary>
    /// Activity'ye event ekler (loglama için).
    /// </summary>
    public static void AddEvent(string eventName)
    {
        Activity.Current?.AddEvent(new ActivityEvent(eventName));
    }
    
    /// <summary>
    /// Activity'ye tag ekler.
    /// </summary>
    public static void AddTag(string key, string value)
    {
        Activity.Current?.SetTag(key, value);
    }
    
    /// <summary>
    /// Activity'ye exception bilgisi ekler.
    /// </summary>
    public static void RecordException(Exception ex)
    {
        Activity.Current?.SetTag("error", true);
        Activity.Current?.SetTag("error.message", ex.Message);
        Activity.Current?.SetTag("error.stack", ex.StackTrace);
    }
}

/// <summary>
/// Telemetry Config - OpenTelemetry yapılandırması
/// </summary>
public class TelemetryConfig
{
    public string ServiceName { get; set; } = "StockOrchestra";
    
    public string JaegerAgentHost { get; set; } = "localhost";
    
    public int JaegerAgentPort { get; set; } = 6831;
    
    public string ConsoleEnabled { get; set; } = "true";
    
    public double SamplingRatio { get; set; } = 1.0;
}

/// <summary>
/// Metrics Keys - Standart metrik anahtarları
/// </summary>
public static class MetricKeys
{
    public const string RequestsTotal = "http.requests.total";
    public const string RequestDuration = "http.request.duration";
    public const string ActiveConnections = "http.connections.active";
    public const string PriceProcessingLatency = "price.processing.latency";
    public const string PricesDiscoveredTotal = "prices.discovered.total";
    public const string ErrorsTotal = "errors.total";
    public const string MemoryUsage = "memory.usage.bytes";
    public const string CpuUsage = "cpu.usage.percent";
}