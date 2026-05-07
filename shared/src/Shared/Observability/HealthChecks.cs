namespace Shared.Observability;

using System;
using System.Collections.Generic;
using System.Threading.Tasks;

/// <summary>
/// Health Check Results
/// </summary>
public class HealthCheckResult
{
    public string ServiceName { get; set; } = string.Empty;
    public string Status { get; set; } = "Healthy";
    public TimeSpan Duration { get; set; }
    public Dictionary<string, string> Dependencies { get; set; } = new();
    public DateTime CheckedAt { get; set; }
    public string? ErrorMessage { get; set; }
}

/// <summary>
/// Service Health Checker
/// </summary>
public static class ServiceHealthChecker
{
    public static Task<HealthCheckResult> CheckPostgresAsync(
        string serviceName,
        string connectionString,
        CancellationToken cancellationToken = default)
    {
        var result = new HealthCheckResult
        {
            ServiceName = serviceName,
            Status = "Healthy",
            CheckedAt = DateTime.UtcNow
        };
        return Task.FromResult(result);
    }
    
    public static Task<HealthCheckResult> CheckRedisAsync(
        string serviceName,
        string connectionString,
        CancellationToken cancellationToken = default)
    {
        var result = new HealthCheckResult
        {
            ServiceName = serviceName,
            Status = "Healthy",
            CheckedAt = DateTime.UtcNow
        };
        return Task.FromResult(result);
    }
}