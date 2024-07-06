using System.Diagnostics;
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
    public bool IsFirstRun { get; private set;  } = true;
    
    /// <summary>
    /// A var which tracks the progress of the pregeneration.
    /// </summary>
    public PreGenerationProgress PregenerationProgress { get; private set; } = new PreGenerationProgress();
    
    private int _queuedPreGenerations = 0;
    private List<Guid> _players = new();
    
    public ProfilePregeneratorService(IServiceScopeFactory scopeFactory, ReplayParserService replayParserService)
    {
        _scopeFactory = scopeFactory;
        _replayParserService = replayParserService;
    }
    
    public Task StartAsync(CancellationToken cancellationToken)
    {
        _replayParserService.OnReplaysFinishedParsing += OnReplaysParsed;
        QueuePregeneration();
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

        var players = new List<Guid>();
        
        foreach (var replay in replays)
        {
            if (replay.RoundEndPlayers == null)
                continue;
            
            foreach (var player in replay.RoundEndPlayers)
            {
                players.Add(player.PlayerGuid);
            }
        }
        
        _players = players.Distinct().ToList();
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
        
        var profilesToGenerate = dbContext.Players
            .Select(x => x.PlayerGuid)
            .Distinct()
            .ToList();

        var alreadyGenerated = dbContext.PlayerProfiles
            .Select(x => x.PlayerGuid)
            .ToList();
        
        profilesToGenerate = profilesToGenerate.Except(alreadyGenerated).ToList();
        profilesToGenerate.AddRange(_players);
        
        PregenerationProgress.Max = profilesToGenerate.Count;
        PregenerationProgress.Current = 0;
        
        foreach (var guid in profilesToGenerate)
        {
            var generated= await replayHelper.GetPlayerProfile(guid, new AuthenticationState(new ClaimsPrincipal()), true);
            Log.Verbose("Pregenerated profile for {Guid}.", guid);
            PregenerationProgress.Current++;
            if (generated != null)
            {
                try
                {
                    dbContext.PlayerProfiles.Add(generated);
                    await dbContext.SaveChangesAsync();
                }
                catch (Exception e)
                {
                    Log.Error(e, "Failed to save pregenerated profile for {Guid}.", guid);
                }
            }
        }

        if (IsFirstRun)
        {
            IsFirstRun = false;
            Log.Information("First run completed.");            
        }
        
        sw.Stop();
        Log.Information("Profile pregeneration finished in {ElapsedMilliseconds}ms.", sw.ElapsedMilliseconds);
        
        if (_queuedPreGenerations > 0)
        {
            _queuedPreGenerations--;
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

