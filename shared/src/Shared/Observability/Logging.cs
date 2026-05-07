namespace Shared.Observability;

using Serilog;

public static class LoggerConfigurator
{
    public static ILogger CreateLogger(LoggerConfig config)
    {
        return new LoggerConfiguration()
            .MinimumLevel.Information()
            .CreateLogger();
    }
}

public class LoggerConfig
{
    public string ServiceName { get; set; } = "StockOrchestra";
}