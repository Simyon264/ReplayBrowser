﻿using System.Diagnostics;
using System.Security.Claims;
using Microsoft.AspNetCore.Components.Authorization;
using ReplayBrowser.Data;
using ReplayBrowser.Helpers;
using Serilog;

namespace ReplayBrowser.Services;

/// <summary>
/// Service that checks every watched profile for every user and pregenerates the profile if it is not already generated for better loading times.
/// </summary>
public class ProfilePregeneratorService : IHostedService, IDisposable, IAsyncDisposable
{
    private Timer? _timer = null;
    private readonly IServiceScopeFactory _scopeFactory;
    
    /// <summary>
    /// List of profiles which get generated even if they are not on a watched profile list. (example being leaderboards)
    /// </summary>
    public List<Guid> AlwaysGenerateProfiles = new List<Guid>();
    
    public ProfilePregeneratorService(IServiceScopeFactory scopeFactory)
    {
        _scopeFactory = scopeFactory;
    }
    
    public Task StartAsync(CancellationToken cancellationToken)
    {
        _timer = new Timer(DoWork, null, TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(30));
        return Task.CompletedTask;
    }
    
    public Task StopAsync(CancellationToken cancellationToken)
    {
        _timer?.Change(Timeout.Infinite, 0);
        return Task.CompletedTask;
    }
    
    private async void DoWork(object? state)
    {
        var sw = new Stopwatch();
        Log.Information("Starting profile pregeneration.");
        sw.Start();
        
        using var scope = _scopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ReplayDbContext>();
        var replayHelper = scope.ServiceProvider.GetRequiredService<ReplayHelper>();
        var profilesToGenerate = new List<Guid>();
        profilesToGenerate.AddRange(AlwaysGenerateProfiles);
        
        dbContext.Accounts.Select(a => a.SavedProfiles).ToList().ForEach(profiles =>
        {
            foreach (var profile in profiles.Where(profile => !profilesToGenerate.Contains(profile)))
            {
                profilesToGenerate.Add(profile);
            }
        });
        
        foreach (var guid in profilesToGenerate)
        {
            await replayHelper.GetPlayerProfile(guid, new AuthenticationState(new ClaimsPrincipal()), true, true);
            Log.Information("Pregenerated profile for {Guid}.", guid);
            // in order to prevent db spamming, we wait a bit before the next profile
            await Task.Delay(10000); // 10 seconds
        }
        sw.Stop();
        Log.Information("Profile pregeneration finished in {ElapsedMilliseconds}ms.", sw.ElapsedMilliseconds);
    }

    public void Dispose()
    {
        _timer?.Dispose();
    }

    public async ValueTask DisposeAsync()
    {
        if (_timer != null) await _timer.DisposeAsync();
    }
}