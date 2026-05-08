using System.Text.Json;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace BallastLane.Api;

internal static class HealthCheckWriters
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = false,
    };

    public static Task WriteJson(HttpContext context, HealthReport report)
    {
        context.Response.ContentType = "application/json";

        var payload = new
        {
            status = report.Status.ToString(),
            totalDurationMs = report.TotalDuration.TotalMilliseconds,
            entries = report.Entries.ToDictionary(
                kvp => kvp.Key,
                kvp => new
                {
                    status = kvp.Value.Status.ToString(),
                    description = kvp.Value.Description,
                    durationMs = kvp.Value.Duration.TotalMilliseconds,
                    tags = kvp.Value.Tags,
                }),
        };

        return context.Response.WriteAsync(JsonSerializer.Serialize(payload, JsonOptions));
    }
}
