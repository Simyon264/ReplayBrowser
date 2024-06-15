using System.ComponentModel.DataAnnotations.Schema;
using System.Diagnostics;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using ReplayBrowser.Data;
using ReplayBrowser.Helpers;
using Serilog;

namespace ReplayBrowser.Services;

/// <summary>
/// Provides the analytics data for the analytics page.
/// </summary>
public class AnalyticsService : IHostedService, IDisposable
{
    private const string CacheKey = "analytics";
    
    private Timer? _timer = null;
    private readonly IMemoryCache _cache;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IConfiguration _config;

    public AnalyticsService(IMemoryCache cache, IServiceScopeFactory scopeFactory, IConfiguration config)
    {
        _cache = cache;
        _scopeFactory = scopeFactory;
        _config = config;
    }
    
    public Task StartAsync(CancellationToken cancellationToken)
    {
        _timer = new Timer(GenerateAnalytics, null, TimeSpan.Zero, TimeSpan.FromHours(12));
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _timer?.Change(Timeout.Infinite, 0);
        return Task.CompletedTask;
    }

    private void GenerateAnalytics(object? state)
    {
        var sw = new Stopwatch();
        sw.Start();
        Log.Information("Generating analytics data...");
        
        using var scope = _scopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ReplayDbContext>();
        var replayUrls = _config.GetSection("ReplayUrls").Get<StorageUrl[]>()!;
        var analyticsData = new AnalyticsData
        {
            Analytics = new List<Analytics>()
        };
        
        var result = dbContext.Database.SqlQueryRaw<DurationResponse>(
            $"""
             SELECT
             	"ServerName",
                 DATE("Replays"."Date") AS date_of_replay,
                 AVG(EXTRACT(EPOCH FROM "Replays"."Date"::time)) / 60 AS average_duration_minutes
             FROM
                 "Replays"
             WHERE
                 EXTRACT(EPOCH FROM "Replays"."Date"::time) / 60 <= 180
                 AND EXTRACT(EPOCH FROM "Replays"."Date"::time) / 60 >= 10
             GROUP BY
             	"ServerName",
                 date_of_replay
             ORDER BY
             	"ServerName",
                 date_of_replay
             """); // why not use EF Core for this? Performance.
        
        // For each in the result, add a new analytics object.
        foreach (var storageUrl in replayUrls)
        {
            var resultsForUrl = result.Where(r => r.ServerName == storageUrl.FallBackServerName).ToList();
            var analytics = new Analytics
            {
                Name = $"{storageUrl.FallBackServerName} ({storageUrl.FallBackServerId})",
                Description = $"Average round duration for {storageUrl.FallBackServerName} in minutes.",
                Type = "bar",
                Data = resultsForUrl.Select(r => new ChartData
                {
                    Label = r.DateOfReplay.ToString("yyyy-MM-dd"),
                    Data = r.AverageDurationMinutes
                }).ToList()
            };
            
            analyticsData.Analytics.Add(analytics);
        }
        
        _cache.Set(CacheKey, analyticsData, new MemoryCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = TimeSpan.FromDays(1)
        });
        
        sw.Stop();
        Log.Information("Generated analytics data in {ElapsedMilliseconds}ms.", sw.ElapsedMilliseconds);
    }
    
    public AnalyticsData GetAnalytics()
    {
        if (!_cache.TryGetValue(CacheKey, out AnalyticsData data))
        {
            throw new InvalidOperationException("The analytics data has not been generated yet.");
        }

        return data;
    }

    private class DurationResponse
    {
        public required string ServerName { get; set; }
        [Column("date_of_replay")]
        public required DateTime DateOfReplay { get; set; }
        [Column("average_duration_minutes")]
        public required double AverageDurationMinutes { get; set; }
    }

    public void Dispose()
    {
        _timer?.Dispose();
        _cache.Dispose();
    }
}