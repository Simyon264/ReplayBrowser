using System.Reflection;
using Microsoft.EntityFrameworkCore;
using ReplayBrowser.Data.Models;
using ReplayBrowser.Data.Models.Account;

namespace ReplayBrowser.Data;

public class ReplayDbContext : DbContext
{
    public ReplayDbContext(DbContextOptions<ReplayDbContext> options) : base(options)
    {
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(Assembly.GetExecutingAssembly());
    }

    public DbSet<Replay> Replays { get; set; }
    public DbSet<Player> Players { get; set; }
    /// <summary>
    /// Stores the parsed replays in a set.
    /// E.g the replay file name.
    /// leviathan-2024_02_18-08_33-round_46751.zip
    /// </summary>
    public DbSet<ParsedReplay> ParsedReplays { get; set; }

    public DbSet<Account> Accounts { get; set; }

    /// <summary>
    /// Contains a list of GUIDs that have requested their data to be removed. Future replays will have this player replaced with "Removed by GDPR request".
    /// </summary>
    public DbSet<GdprRequest> GdprRequests { get; set; }

    /// <summary>
    /// Contains a list of notices that are displayed to every user if the condition is met.
    /// </summary>
    public DbSet<Notice> Notices { get; set; }

    /// <summary>
    /// Cached player data.
    /// </summary>
    /// <returns></returns>
    public DbSet<CollectedPlayerData> PlayerProfiles { get; set; }
}