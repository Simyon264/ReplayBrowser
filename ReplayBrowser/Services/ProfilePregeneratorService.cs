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

    private Queue<List<Guid>> _queue = new();
    private Timer? _timer = null;
    private bool _running = false;
    
    /// <summary>
    /// A var which tracks the progress of the pregeneration.
    /// </summary>
    public PreGenerationProgress PregenerationProgress { get; private set; } = new PreGenerationProgress();
    public ProfilePregeneratorService(IServiceScopeFactory scopeFactory, ReplayParserService replayParserService)
    {
        _scopeFactory = scopeFactory;
        _replayParserService = replayParserService;
        _queue.Enqueue([]);
        _timer = new Timer(_ => PregenerateProfiles(), null, TimeSpan.Zero, TimeSpan.FromMinutes(5));
    }
    
    public Task StartAsync(CancellationToken cancellationToken)
    {
        _replayParserService.OnReplaysFinishedParsing += OnReplaysParsed;
        _queue.Enqueue(new List<Guid>());
        return Task.CompletedTask;
    }
    
    public Task StopAsync(CancellationToken cancellationToken)
    {
        _replayParserService.OnReplaysFinishedParsing -= OnReplaysParsed;
        return Task.CompletedTask;
    }

    private void OnReplaysParsed(object? sender, List<Replay> replays)
    {
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
        
        players = players.Distinct().ToList();
        
        _queue.Enqueue(players);
    }

    private async void PregenerateProfiles()
    {
        if (_running)
            return;
        _running = true;
        
        while (_queue.TryDequeue(out var players))
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
            profilesToGenerate.AddRange(players);
            profilesToGenerate = profilesToGenerate.Distinct().ToList();
            
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
                        if (dbContext.PlayerProfiles.Any(x => x.PlayerGuid == guid))
                        {
                            // Get the existing profile and update it
                            var existing = dbContext.PlayerProfiles.First(x => x.PlayerGuid == guid);
                            existing.GeneratedAt = generated.GeneratedAt;
                            existing.PlayerGuid = generated.PlayerGuid;
                            existing.PlayerData = generated.PlayerData;
                            existing.Characters = generated.Characters;
                            existing.TotalEstimatedPlaytime = generated.TotalEstimatedPlaytime;
                            existing.TotalRoundsPlayed = generated.TotalRoundsPlayed;
                            existing.TotalAntagRoundsPlayed = generated.TotalAntagRoundsPlayed;
                            existing.JobCount = generated.JobCount;
                            existing.LastSeen = generated.LastSeen;
                            existing.IsWatched = generated.IsWatched;
                        }
                        else
                        {
                            dbContext.PlayerProfiles.Add(generated);
                        }
                        await dbContext.SaveChangesAsync();
                    }
                    catch (Exception e)
                    {
                        Log.Error(e, "Failed to save pregenerated profile for {Guid}.", guid);
                    }
                }
            }
            
            sw.Stop();
            Log.Information("Profile pregeneration finished in {ElapsedMilliseconds}ms.", sw.ElapsedMilliseconds);
        }
        
        _running = false;
    }
    
    public class PreGenerationProgress
    {
        public int Current { get; set; } = 0;
        public int Max { get; set; } = 0;
    }
}

