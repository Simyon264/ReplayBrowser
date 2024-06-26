﻿using System.Diagnostics;
using System.Security.Claims;
using Microsoft.AspNetCore.Components.Authorization;
using ReplayBrowser.Data;
using ReplayBrowser.Data.Models;
using ReplayBrowser.Helpers;
using ReplayBrowser.Services.ReplayParser;
using Serilog;

namespace ReplayBrowser.Services;

/// <summary>
/// Service that checks every watched profile for every user and pregenerates the profile if it is not already generated for better loading times.
/// </summary>
public class ProfilePregeneratorService : IHostedService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private ReplayParserService _replayParserService;
    
    public bool IsFirstRun { get; private set;  }= true;
    
    /// <summary>
    /// List of profiles that have been generated.
    /// </summary>
    private readonly List<Guid> _generatedProfiles = new(); 
    
    /// <summary>
    /// List of profiles which get generated even if they are not on a watched profile list. (example being leaderboards)
    /// </summary>
    public List<Guid> AlwaysGenerateProfiles = new List<Guid>();
    
    /// <summary>
    /// A var which tracks the progress of the pregeneration.
    /// </summary>
    public PreGenerationProgress PregenerationProgress { get; private set; } = new PreGenerationProgress();
    
    private int _queuedPreGenerations = 0;
    
    public ProfilePregeneratorService(IServiceScopeFactory scopeFactory, ReplayParserService replayParserService)
    {
        _scopeFactory = scopeFactory;
        _replayParserService = replayParserService;
    }
    
    public Task StartAsync(CancellationToken cancellationToken)
    {
        _replayParserService.OnReplaysFinishedParsing += OnReplaysParsed;
        
        // In one minute, start the pregeneration.
        Task.Delay(TimeSpan.FromMinutes(1), cancellationToken).ContinueWith(t => QueuePregeneration(), cancellationToken);
        return Task.CompletedTask;
    }
    
    public Task StopAsync(CancellationToken cancellationToken)
    {
        _replayParserService.OnReplaysFinishedParsing -= OnReplaysParsed;
        return Task.CompletedTask;
    }

    private void OnReplaysParsed(object? sender, List<Replay> replays)
    {
        if (IsFirstRun)
        {
            // We are waiting for the first run to complete
            Log.Warning("Triggered OnReplaysParsed before the first run has completed.");
            return;
        }

        foreach (var replay in replays)
        {
            if (replay.RoundEndPlayers == null)
                continue;
            
            foreach (var player in replay.RoundEndPlayers)
            {
                if (!_generatedProfiles.Contains(player.PlayerGuid))
                    // Profile is not getting autogenerated, ignore.
                    continue; 
                
                // Profile is getting autogenerated, remove it from the list.
                _generatedProfiles.Remove(player.PlayerGuid);
            }
        }
        
        QueuePregeneration();
    }

    private void QueuePregeneration()
    {
        _queuedPreGenerations++;

        if (_queuedPreGenerations == 1) // If we are the first one, start the pregeneration.
            PregenerateProfiles();
    }

    private async void PregenerateProfiles()
    {
        var sw = new Stopwatch();
        Log.Information("Starting profile pregeneration.");
        sw.Start();
        
        using var scope = _scopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ReplayDbContext>();
        var replayHelper = scope.ServiceProvider.GetRequiredService<ReplayHelper>();
        var profilesToGenerate = new List<Guid>();
        profilesToGenerate.AddRange(AlwaysGenerateProfiles);
        
        await foreach (var account in dbContext.Accounts)
        {
            foreach (var profile in account.SavedProfiles)
            {
                if (profilesToGenerate.Contains(profile)) continue;
                profilesToGenerate.Add(profile);
            }
        }
        
        // Remove every profile already found in _generatedProfiles
        profilesToGenerate = profilesToGenerate.Except(_generatedProfiles).ToList();
        PregenerationProgress.Max = profilesToGenerate.Count;
        PregenerationProgress.Current = 0;
        
        foreach (var guid in profilesToGenerate)
        {
            await replayHelper.GetPlayerProfile(guid, new AuthenticationState(new ClaimsPrincipal()), TimeSpan.FromDays(999), true, true);
            Log.Information("Pregenerated profile for {Guid}.", guid);
            _generatedProfiles.Add(guid);
            PregenerationProgress.Current++;
        }

        if (IsFirstRun)
        {
            IsFirstRun = false;
            Log.Information("First run completed.");            
        }
        
        sw.Stop();
        Log.Information("Profile pregeneration finished in {ElapsedMilliseconds}ms.", sw.ElapsedMilliseconds);
        
        _queuedPreGenerations--;
        if (_queuedPreGenerations > 0)
        {
            PregenerateProfiles(); // this is stupid, but then again, this whole codebase is, and its not like someone else is going to read this. Right?
            Log.Information("Pregeneration queued.");
        }
    }
    
    public class PreGenerationProgress
    {
        public int Current { get; set; } = 0;
        public int Max { get; set; } = 0;
    }
}

