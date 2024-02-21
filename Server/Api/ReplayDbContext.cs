﻿using Microsoft.EntityFrameworkCore;
using Shared.Models;

namespace Server.Api;

public class ReplayDbContext : DbContext
{
    public ReplayDbContext(DbContextOptions<ReplayDbContext> options) : base(options)
    {
    }
    
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Replay>()
            .HasKey(r => r.Id); // Using Id as primary key
        modelBuilder.Entity<Player>()
            .HasKey(p => p.Id); // Using Id as primary key
        
        modelBuilder.Entity<Replay>().ToTable("Replays");
        modelBuilder.Entity<Player>().ToTable("Players");
        modelBuilder.Entity<ParsedReplay>().ToTable("ParsedReplays");
    }
    
    public DbSet<Replay> Replays { get; set; }
    public DbSet<Player> Players { get; set; }
    /// <summary>
    /// Stores the parsed replays in a set.
    /// E.g the replay file name.
    /// leviathan-2024_02_18-08_33-round_46751.zip
    /// </summary>
    public DbSet<ParsedReplay> ParsedReplays { get; set; }
}