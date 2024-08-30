﻿// <auto-generated />
using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;
using NpgsqlTypes;
using ReplayBrowser.Data;

#nullable disable

namespace Server.Migrations
{
    [DbContext(typeof(ReplayDbContext))]
    [Migration("20240829003832_ReplayEventsSubtypes")]
    partial class ReplayEventsSubtypes
    {
        /// <inheritdoc />
        protected override void BuildTargetModel(ModelBuilder modelBuilder)
        {
#pragma warning disable 612, 618
            modelBuilder
                .HasAnnotation("ProductVersion", "8.0.2")
                .HasAnnotation("Relational:MaxIdentifierLength", 63);

            NpgsqlModelBuilderExtensions.UseIdentityByDefaultColumns(modelBuilder);

            modelBuilder.Entity("ReplayBrowser.Data.Models.Account.Account", b =>
                {
                    b.Property<int>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("integer");

                    NpgsqlPropertyBuilderExtensions.UseIdentityByDefaultColumn(b.Property<int>("Id"));

                    b.Property<List<int>>("FavoriteReplays")
                        .IsRequired()
                        .HasColumnType("integer[]");

                    b.Property<Guid>("Guid")
                        .HasColumnType("uuid");

                    b.Property<bool>("IsAdmin")
                        .HasColumnType("boolean");

                    b.Property<bool>("Protected")
                        .HasColumnType("boolean");

                    b.Property<List<Guid>>("SavedProfiles")
                        .IsRequired()
                        .HasColumnType("uuid[]");

                    b.Property<int>("SettingsId")
                        .HasColumnType("integer");

                    b.Property<string>("Username")
                        .IsRequired()
                        .HasColumnType("text");

                    b.HasKey("Id");

                    b.HasIndex("Guid")
                        .IsUnique();

                    b.HasIndex("SettingsId");

                    b.HasIndex("Username");

                    b.ToTable("Accounts");
                });

            modelBuilder.Entity("ReplayBrowser.Data.Models.Account.AccountSettings", b =>
                {
                    b.Property<int>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("integer");

                    NpgsqlPropertyBuilderExtensions.UseIdentityByDefaultColumn(b.Property<int>("Id"));

                    b.Property<List<Guid>>("Friends")
                        .IsRequired()
                        .HasColumnType("uuid[]");

                    b.Property<bool>("RedactInformation")
                        .HasColumnType("boolean");

                    b.HasKey("Id");

                    b.ToTable("AccountSettings");
                });

            modelBuilder.Entity("ReplayBrowser.Data.Models.Account.HistoryEntry", b =>
                {
                    b.Property<int>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("integer");

                    NpgsqlPropertyBuilderExtensions.UseIdentityByDefaultColumn(b.Property<int>("Id"));

                    b.Property<int?>("AccountId")
                        .HasColumnType("integer");

                    b.Property<string>("Action")
                        .IsRequired()
                        .HasColumnType("text");

                    b.Property<string>("Details")
                        .HasColumnType("text");

                    b.Property<DateTime>("Time")
                        .HasColumnType("timestamp with time zone");

                    b.HasKey("Id");

                    b.HasIndex("AccountId");

                    b.ToTable("HistoryEntry");
                });

            modelBuilder.Entity("ReplayBrowser.Data.Models.Account.Webhook", b =>
                {
                    b.Property<int>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("integer");

                    NpgsqlPropertyBuilderExtensions.UseIdentityByDefaultColumn(b.Property<int>("Id"));

                    b.Property<int?>("AccountId")
                        .HasColumnType("integer");

                    b.Property<string>("Servers")
                        .IsRequired()
                        .HasColumnType("text");

                    b.Property<byte>("Type")
                        .HasColumnType("smallint");

                    b.Property<string>("Url")
                        .IsRequired()
                        .HasColumnType("text");

                    b.HasKey("Id");

                    b.HasIndex("AccountId");

                    b.ToTable("Webhook");
                });

            modelBuilder.Entity("ReplayBrowser.Data.Models.Account.WebhookHistory", b =>
                {
                    b.Property<int>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("integer");

                    NpgsqlPropertyBuilderExtensions.UseIdentityByDefaultColumn(b.Property<int>("Id"));

                    b.Property<string>("ResponseBody")
                        .IsRequired()
                        .HasColumnType("text");

                    b.Property<int>("ResponseCode")
                        .HasColumnType("integer");

                    b.Property<DateTime>("SentAt")
                        .HasColumnType("timestamp with time zone");

                    b.Property<int>("WebhookId")
                        .HasColumnType("integer");

                    b.HasKey("Id");

                    b.HasIndex("WebhookId");

                    b.ToTable("WebhookHistory");
                });

            modelBuilder.Entity("ReplayBrowser.Data.Models.CharacterData", b =>
                {
                    b.Property<int>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("integer");

                    NpgsqlPropertyBuilderExtensions.UseIdentityByDefaultColumn(b.Property<int>("Id"));

                    b.Property<string>("CharacterName")
                        .IsRequired()
                        .HasColumnType("text");

                    b.Property<Guid?>("CollectedPlayerDataPlayerGuid")
                        .HasColumnType("uuid");

                    b.Property<DateTime>("LastPlayed")
                        .HasColumnType("timestamp with time zone");

                    b.Property<int>("RoundsPlayed")
                        .HasColumnType("integer");

                    b.HasKey("Id");

                    b.HasIndex("CollectedPlayerDataPlayerGuid");

                    b.ToTable("CharacterData");
                });

            modelBuilder.Entity("ReplayBrowser.Data.Models.CollectedPlayerData", b =>
                {
                    b.Property<Guid>("PlayerGuid")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("uuid");

                    b.Property<DateTime>("GeneratedAt")
                        .HasColumnType("timestamp with time zone");

                    b.Property<bool>("IsWatched")
                        .HasColumnType("boolean");

                    b.Property<DateTime>("LastSeen")
                        .HasColumnType("timestamp with time zone");

                    b.Property<int>("PlayerDataId")
                        .HasColumnType("integer");

                    b.Property<int>("TotalAntagRoundsPlayed")
                        .HasColumnType("integer");

                    b.Property<TimeSpan>("TotalEstimatedPlaytime")
                        .HasColumnType("interval");

                    b.Property<int>("TotalRoundsPlayed")
                        .HasColumnType("integer");

                    b.HasKey("PlayerGuid");

                    b.HasIndex("PlayerDataId");

                    b.HasIndex("PlayerGuid")
                        .IsUnique();

                    b.ToTable("PlayerProfiles");
                });

            modelBuilder.Entity("ReplayBrowser.Data.Models.GdprRequest", b =>
                {
                    b.Property<Guid>("Guid")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("uuid");

                    b.HasKey("Guid");

                    b.ToTable("GdprRequests");
                });

            modelBuilder.Entity("ReplayBrowser.Data.Models.JobCountData", b =>
                {
                    b.Property<int>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("integer");

                    NpgsqlPropertyBuilderExtensions.UseIdentityByDefaultColumn(b.Property<int>("Id"));

                    b.Property<Guid?>("CollectedPlayerDataPlayerGuid")
                        .HasColumnType("uuid");

                    b.Property<string>("JobPrototype")
                        .IsRequired()
                        .HasColumnType("text");

                    b.Property<DateTime>("LastPlayed")
                        .HasColumnType("timestamp with time zone");

                    b.Property<int>("RoundsPlayed")
                        .HasColumnType("integer");

                    b.HasKey("Id");

                    b.HasIndex("CollectedPlayerDataPlayerGuid");

                    b.ToTable("JobCountData");
                });

            modelBuilder.Entity("ReplayBrowser.Data.Models.JobDepartment", b =>
                {
                    b.Property<int>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("integer");

                    NpgsqlPropertyBuilderExtensions.UseIdentityByDefaultColumn(b.Property<int>("Id"));

                    b.Property<string>("Department")
                        .IsRequired()
                        .HasColumnType("text");

                    b.Property<string>("Job")
                        .IsRequired()
                        .HasColumnType("text");

                    b.HasKey("Id");

                    b.HasIndex("Job")
                        .IsUnique();

                    b.ToTable("JobDepartments");
                });

            modelBuilder.Entity("ReplayBrowser.Data.Models.Notice", b =>
                {
                    b.Property<int?>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("integer");

                    NpgsqlPropertyBuilderExtensions.UseIdentityByDefaultColumn(b.Property<int?>("Id"));

                    b.Property<DateTime>("EndDate")
                        .HasColumnType("timestamp with time zone");

                    b.Property<string>("Message")
                        .IsRequired()
                        .HasColumnType("text");

                    b.Property<DateTime>("StartDate")
                        .HasColumnType("timestamp with time zone");

                    b.Property<string>("Title")
                        .IsRequired()
                        .HasColumnType("text");

                    b.HasKey("Id");

                    b.ToTable("Notices");
                });

            modelBuilder.Entity("ReplayBrowser.Data.Models.ParsedReplay", b =>
                {
                    b.Property<string>("Name")
                        .HasColumnType("text");

                    b.HasKey("Name");

                    b.ToTable("ParsedReplays");
                });

            modelBuilder.Entity("ReplayBrowser.Data.Models.Player", b =>
                {
                    b.Property<int>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("integer");

                    NpgsqlPropertyBuilderExtensions.UseIdentityByDefaultColumn(b.Property<int>("Id"));

                    b.Property<bool>("Antag")
                        .HasColumnType("boolean");

                    b.Property<List<string>>("AntagPrototypes")
                        .IsRequired()
                        .HasColumnType("text[]");

                    b.Property<int?>("EffectiveJobId")
                        .HasColumnType("integer");

                    b.Property<List<string>>("JobPrototypes")
                        .IsRequired()
                        .HasColumnType("text[]");

                    b.Property<int>("ParticipantId")
                        .HasColumnType("integer");

                    b.Property<string>("PlayerIcName")
                        .IsRequired()
                        .HasColumnType("text");

                    b.HasKey("Id");

                    b.HasIndex("EffectiveJobId");

                    b.HasIndex("ParticipantId");

                    b.HasIndex("PlayerIcName");

                    b.ToTable("Players");
                });

            modelBuilder.Entity("ReplayBrowser.Data.Models.PlayerData", b =>
                {
                    b.Property<int>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("integer");

                    NpgsqlPropertyBuilderExtensions.UseIdentityByDefaultColumn(b.Property<int>("Id"));

                    b.Property<Guid?>("PlayerGuid")
                        .HasColumnType("uuid");

                    b.Property<string>("Username")
                        .IsRequired()
                        .HasColumnType("text");

                    b.HasKey("Id");

                    b.ToTable("PlayerData");
                });

            modelBuilder.Entity("ReplayBrowser.Data.Models.Replay", b =>
                {
                    b.Property<int>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("integer");

                    NpgsqlPropertyBuilderExtensions.UseIdentityByDefaultColumn(b.Property<int>("Id"));

                    b.Property<DateTime?>("Date")
                        .HasColumnType("timestamp with time zone");

                    b.Property<string>("Duration")
                        .IsRequired()
                        .HasColumnType("text");

                    b.Property<int>("EndTick")
                        .HasColumnType("integer");

                    b.Property<string>("EndTime")
                        .IsRequired()
                        .HasColumnType("text");

                    b.Property<int>("FileCount")
                        .HasColumnType("integer");

                    b.Property<string>("Gamemode")
                        .IsRequired()
                        .HasColumnType("text");

                    b.Property<string>("Link")
                        .IsRequired()
                        .HasColumnType("text");

                    b.Property<string>("Map")
                        .HasColumnType("text");

                    b.Property<List<string>>("Maps")
                        .HasColumnType("text[]");

                    b.Property<string>("RoundEndText")
                        .HasColumnType("text");

                    b.Property<NpgsqlTsVector>("RoundEndTextSearchVector")
                        .IsRequired()
                        .ValueGeneratedOnAddOrUpdate()
                        .HasColumnType("tsvector")
                        .HasAnnotation("Npgsql:TsVectorConfig", "english")
                        .HasAnnotation("Npgsql:TsVectorProperties", new[] { "RoundEndText" });

                    b.Property<int?>("RoundId")
                        .HasColumnType("integer");

                    b.Property<string>("ServerId")
                        .IsRequired()
                        .HasColumnType("text");

                    b.Property<string>("ServerName")
                        .HasColumnType("text");

                    b.Property<int>("Size")
                        .HasColumnType("integer");

                    b.Property<int>("UncompressedSize")
                        .HasColumnType("integer");

                    b.HasKey("Id");

                    b.HasIndex("Gamemode");

                    b.HasIndex("Map");

                    b.HasIndex("RoundEndTextSearchVector");

                    NpgsqlIndexBuilderExtensions.HasMethod(b.HasIndex("RoundEndTextSearchVector"), "GIN");

                    b.HasIndex("ServerId");

                    b.HasIndex("ServerName");

                    b.ToTable("Replays");
                });

            modelBuilder.Entity("ReplayBrowser.Data.Models.ReplayDbEvent", b =>
                {
                    b.Property<int>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("integer");

                    NpgsqlPropertyBuilderExtensions.UseIdentityByDefaultColumn(b.Property<int>("Id"));

                    b.Property<string>("ClassType")
                        .IsRequired()
                        .HasMaxLength(34)
                        .HasColumnType("character varying(34)");

                    b.Property<int>("EventType")
                        .HasColumnType("integer");

                    b.Property<string>("NearestBeacon")
                        .HasColumnType("text");

                    b.Property<NpgsqlPoint>("Position")
                        .HasColumnType("point");

                    b.Property<int>("ReplayId")
                        .HasColumnType("integer");

                    b.Property<int>("Severity")
                        .HasColumnType("integer");

                    b.Property<double?>("Time")
                        .IsRequired()
                        .HasColumnType("double precision");

                    b.HasKey("Id");

                    b.HasIndex("EventType");

                    b.HasIndex("ReplayId");

                    b.HasIndex("Severity");

                    b.HasIndex("Time");

                    b.ToTable("ReplayDbEvent");

                    b.HasDiscriminator<string>("ClassType").HasValue("ReplayDbEvent");

                    b.UseTphMappingStrategy();
                });

            modelBuilder.Entity("ReplayBrowser.Data.Models.ReplayParticipant", b =>
                {
                    b.Property<int>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("integer");

                    NpgsqlPropertyBuilderExtensions.UseIdentityByDefaultColumn(b.Property<int>("Id"));

                    b.Property<Guid>("PlayerGuid")
                        .HasColumnType("uuid");

                    b.Property<int>("ReplayId")
                        .HasColumnType("integer");

                    b.Property<string>("Username")
                        .IsRequired()
                        .HasColumnType("text");

                    b.HasKey("Id");

                    b.HasIndex("ReplayId");

                    b.HasIndex("Username");

                    b.HasIndex("PlayerGuid", "ReplayId")
                        .IsUnique();

                    b.ToTable("ReplayParticipants");
                });

            modelBuilder.Entity("ReplayBrowser.Models.Ingested.ReplayEvents.ReplayEventPlayer", b =>
                {
                    b.Property<int>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("integer");

                    NpgsqlPropertyBuilderExtensions.UseIdentityByDefaultColumn(b.Property<int>("Id"));

                    b.Property<string[]>("AntagPrototypes")
                        .IsRequired()
                        .HasColumnType("text[]");

                    b.Property<string[]>("JobPrototypes")
                        .IsRequired()
                        .HasColumnType("text[]");

                    b.Property<Guid?>("PlayerGuid")
                        .IsRequired()
                        .HasColumnType("uuid");

                    b.Property<string>("PlayerIcName")
                        .IsRequired()
                        .HasColumnType("text");

                    b.Property<string>("PlayerOocName")
                        .IsRequired()
                        .HasColumnType("text");

                    b.HasKey("Id");

                    b.ToTable("ReplayEventPlayer");
                });

            modelBuilder.Entity("ReplayBrowser.Models.Ingested.ReplayEvents.EventTypes.AlertLevelChangedReplayEvent", b =>
                {
                    b.HasBaseType("ReplayBrowser.Data.Models.ReplayDbEvent");

                    b.Property<string>("AlertLevel")
                        .IsRequired()
                        .HasColumnType("text");

                    b.HasDiscriminator().HasValue("AlertLevelChangedReplayEvent");
                });

            modelBuilder.Entity("ReplayBrowser.Models.Ingested.ReplayEvents.EventTypes.CargoObjectSoldReplayEvent", b =>
                {
                    b.HasBaseType("ReplayBrowser.Data.Models.ReplayDbEvent");

                    b.Property<double>("Amount")
                        .HasColumnType("double precision");

                    b.Property<int>("ObjectsSold")
                        .HasColumnType("integer");

                    b.HasDiscriminator().HasValue("CargoObjectSoldReplayEvent");
                });

            modelBuilder.Entity("ReplayBrowser.Models.Ingested.ReplayEvents.EventTypes.CargoProductsOrderedReplayEvent", b =>
                {
                    b.HasBaseType("ReplayBrowser.Data.Models.ReplayDbEvent");

                    b.Property<string>("ApprovedBy")
                        .IsRequired()
                        .HasColumnType("text");

                    b.Property<string>("Products")
                        .IsRequired()
                        .HasColumnType("text");

                    b.HasDiscriminator().HasValue("CargoProductsOrderedReplayEvent");
                });

            modelBuilder.Entity("ReplayBrowser.Models.Ingested.ReplayEvents.EventTypes.ChatAnnouncementReplayEvent", b =>
                {
                    b.HasBaseType("ReplayBrowser.Data.Models.ReplayDbEvent");

                    b.Property<string>("Message")
                        .IsRequired()
                        .HasColumnType("text");

                    b.Property<string>("Sender")
                        .IsRequired()
                        .HasColumnType("text");

                    b.HasDiscriminator().HasValue("ChatAnnouncementReplayEvent");
                });

            modelBuilder.Entity("ReplayBrowser.Models.Ingested.ReplayEvents.EventTypes.ChatMessageReplayEvent", b =>
                {
                    b.HasBaseType("ReplayBrowser.Data.Models.ReplayDbEvent");

                    b.Property<string>("Message")
                        .IsRequired()
                        .HasColumnType("text");

                    b.Property<string>("Sender")
                        .IsRequired()
                        .HasColumnType("text");

                    b.Property<string>("Type")
                        .IsRequired()
                        .HasColumnType("text");

                    b.ToTable("ReplayDbEvent", t =>
                        {
                            t.Property("Message")
                                .HasColumnName("ChatMessageReplayEvent_Message");

                            t.Property("Sender")
                                .HasColumnName("ChatMessageReplayEvent_Sender");
                        });

                    b.HasDiscriminator().HasValue("ChatMessageReplayEvent");
                });

            modelBuilder.Entity("ReplayBrowser.Models.Ingested.ReplayEvents.EventTypes.GenericObjectEvent", b =>
                {
                    b.HasBaseType("ReplayBrowser.Data.Models.ReplayDbEvent");

                    b.Property<string>("Origin")
                        .HasColumnType("text");

                    b.Property<string>("Target")
                        .IsRequired()
                        .HasColumnType("text");

                    b.HasDiscriminator().HasValue("GenericObjectEvent");
                });

            modelBuilder.Entity("ReplayBrowser.Models.Ingested.ReplayEvents.EventTypes.GenericPlayerEvent", b =>
                {
                    b.HasBaseType("ReplayBrowser.Data.Models.ReplayDbEvent");

                    b.Property<string>("Origin")
                        .HasColumnType("text");

                    b.Property<string>("Target")
                        .IsRequired()
                        .HasColumnType("text");

                    b.ToTable("ReplayDbEvent", t =>
                        {
                            t.Property("Origin")
                                .HasColumnName("GenericPlayerEvent_Origin");

                            t.Property("Target")
                                .HasColumnName("GenericPlayerEvent_Target");
                        });

                    b.HasDiscriminator().HasValue("GenericPlayerEvent");
                });

            modelBuilder.Entity("ReplayBrowser.Models.Ingested.ReplayEvents.EventTypes.MobStateChangedNPCReplayEvent", b =>
                {
                    b.HasBaseType("ReplayBrowser.Data.Models.ReplayDbEvent");

                    b.Property<byte>("NewState")
                        .HasColumnType("smallint");

                    b.Property<byte>("OldState")
                        .HasColumnType("smallint");

                    b.Property<string>("Target")
                        .IsRequired()
                        .HasColumnType("text");

                    b.ToTable("ReplayDbEvent", t =>
                        {
                            t.Property("Target")
                                .HasColumnName("MobStateChangedNPCReplayEvent_Target");
                        });

                    b.HasDiscriminator().HasValue("MobStateChangedNPCReplayEvent");
                });

            modelBuilder.Entity("ReplayBrowser.Models.Ingested.ReplayEvents.EventTypes.MobStateChangedPlayerReplayEvent", b =>
                {
                    b.HasBaseType("ReplayBrowser.Data.Models.ReplayDbEvent");

                    b.Property<byte>("NewState")
                        .HasColumnType("smallint");

                    b.Property<byte>("OldState")
                        .HasColumnType("smallint");

                    b.Property<string>("Target")
                        .IsRequired()
                        .HasColumnType("text");

                    b.ToTable("ReplayDbEvent", t =>
                        {
                            t.Property("NewState")
                                .HasColumnName("MobStateChangedPlayerReplayEvent_NewState");

                            t.Property("OldState")
                                .HasColumnName("MobStateChangedPlayerReplayEvent_OldState");

                            t.Property("Target")
                                .HasColumnName("MobStateChangedPlayerReplayEvent_Target");
                        });

                    b.HasDiscriminator().HasValue("MobStateChangedPlayerReplayEvent");
                });

            modelBuilder.Entity("ReplayBrowser.Models.Ingested.ReplayEvents.EventTypes.NewsArticlePublishedReplayEvent", b =>
                {
                    b.HasBaseType("ReplayBrowser.Data.Models.ReplayDbEvent");

                    b.Property<string>("Author")
                        .HasColumnType("text");

                    b.Property<string>("Content")
                        .IsRequired()
                        .HasColumnType("text");

                    b.Property<TimeSpan>("ShareTime")
                        .HasColumnType("interval");

                    b.Property<string>("Title")
                        .IsRequired()
                        .HasColumnType("text");

                    b.HasDiscriminator().HasValue("NewsArticlePublishedReplayEvent");
                });

            modelBuilder.Entity("ReplayBrowser.Models.Ingested.ReplayEvents.EventTypes.ReplayExplosionEvent", b =>
                {
                    b.HasBaseType("ReplayBrowser.Data.Models.ReplayDbEvent");

                    b.Property<bool>("CanCreateVacuum")
                        .HasColumnType("boolean");

                    b.Property<float>("Intensity")
                        .HasColumnType("real");

                    b.Property<int>("MaxTileBreak")
                        .HasColumnType("integer");

                    b.Property<float>("MaxTileIntensity")
                        .HasColumnType("real");

                    b.Property<float>("Slope")
                        .HasColumnType("real");

                    b.Property<string>("Source")
                        .HasColumnType("text");

                    b.Property<float>("TileBreakScale")
                        .HasColumnType("real");

                    b.Property<string>("Type")
                        .IsRequired()
                        .HasColumnType("text");

                    b.ToTable("ReplayDbEvent", t =>
                        {
                            t.Property("Type")
                                .HasColumnName("ReplayExplosionEvent_Type");
                        });

                    b.HasDiscriminator().HasValue("ReplayExplosionEvent");
                });

            modelBuilder.Entity("ReplayBrowser.Models.Ingested.ReplayEvents.EventTypes.ShuttleReplayEvent", b =>
                {
                    b.HasBaseType("ReplayBrowser.Data.Models.ReplayDbEvent");

                    b.Property<int?>("Countdown")
                        .HasColumnType("integer");

                    b.Property<string>("Source")
                        .HasColumnType("text");

                    b.ToTable("ReplayDbEvent", t =>
                        {
                            t.Property("Source")
                                .HasColumnName("ShuttleReplayEvent_Source");
                        });

                    b.HasDiscriminator().HasValue("ShuttleReplayEvent");
                });

            modelBuilder.Entity("ReplayBrowser.Models.Ingested.ReplayEvents.EventTypes.StoreBuyReplayEvent", b =>
                {
                    b.HasBaseType("ReplayBrowser.Data.Models.ReplayDbEvent");

                    b.Property<string>("Buyer")
                        .IsRequired()
                        .HasColumnType("text");

                    b.Property<int>("Cost")
                        .HasColumnType("integer");

                    b.Property<string>("Item")
                        .IsRequired()
                        .HasColumnType("text");

                    b.HasDiscriminator().HasValue("StoreBuyReplayEvent");
                });

            modelBuilder.Entity("ReplayBrowser.Models.Ingested.ReplayEvents.EventTypes.TechnologyUnlockedReplayEvent", b =>
                {
                    b.HasBaseType("ReplayBrowser.Data.Models.ReplayDbEvent");

                    b.Property<string>("Discipline")
                        .IsRequired()
                        .HasColumnType("text");

                    b.Property<string>("Name")
                        .IsRequired()
                        .HasColumnType("text");

                    b.Property<string>("Player")
                        .IsRequired()
                        .HasColumnType("text");

                    b.Property<int>("Tier")
                        .HasColumnType("integer");

                    b.HasDiscriminator().HasValue("TechnologyUnlockedReplayEvent");
                });

            modelBuilder.Entity("ReplayBrowser.Data.Models.Account.Account", b =>
                {
                    b.HasOne("ReplayBrowser.Data.Models.Account.AccountSettings", "Settings")
                        .WithMany()
                        .HasForeignKey("SettingsId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();

                    b.Navigation("Settings");
                });

            modelBuilder.Entity("ReplayBrowser.Data.Models.Account.HistoryEntry", b =>
                {
                    b.HasOne("ReplayBrowser.Data.Models.Account.Account", "Account")
                        .WithMany("History")
                        .HasForeignKey("AccountId");

                    b.Navigation("Account");
                });

            modelBuilder.Entity("ReplayBrowser.Data.Models.Account.Webhook", b =>
                {
                    b.HasOne("ReplayBrowser.Data.Models.Account.Account", null)
                        .WithMany("Webhooks")
                        .HasForeignKey("AccountId");
                });

            modelBuilder.Entity("ReplayBrowser.Data.Models.Account.WebhookHistory", b =>
                {
                    b.HasOne("ReplayBrowser.Data.Models.Account.Webhook", "Webhook")
                        .WithMany("Logs")
                        .HasForeignKey("WebhookId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();

                    b.Navigation("Webhook");
                });

            modelBuilder.Entity("ReplayBrowser.Data.Models.CharacterData", b =>
                {
                    b.HasOne("ReplayBrowser.Data.Models.CollectedPlayerData", null)
                        .WithMany("Characters")
                        .HasForeignKey("CollectedPlayerDataPlayerGuid");
                });

            modelBuilder.Entity("ReplayBrowser.Data.Models.CollectedPlayerData", b =>
                {
                    b.HasOne("ReplayBrowser.Data.Models.PlayerData", "PlayerData")
                        .WithMany()
                        .HasForeignKey("PlayerDataId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();

                    b.Navigation("PlayerData");
                });

            modelBuilder.Entity("ReplayBrowser.Data.Models.JobCountData", b =>
                {
                    b.HasOne("ReplayBrowser.Data.Models.CollectedPlayerData", null)
                        .WithMany("JobCount")
                        .HasForeignKey("CollectedPlayerDataPlayerGuid");
                });

            modelBuilder.Entity("ReplayBrowser.Data.Models.Player", b =>
                {
                    b.HasOne("ReplayBrowser.Data.Models.JobDepartment", "EffectiveJob")
                        .WithMany()
                        .HasForeignKey("EffectiveJobId");

                    b.HasOne("ReplayBrowser.Data.Models.ReplayParticipant", "Participant")
                        .WithMany("Players")
                        .HasForeignKey("ParticipantId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();

                    b.Navigation("EffectiveJob");

                    b.Navigation("Participant");
                });

            modelBuilder.Entity("ReplayBrowser.Data.Models.ReplayDbEvent", b =>
                {
                    b.HasOne("ReplayBrowser.Data.Models.Replay", "Replay")
                        .WithMany("Events")
                        .HasForeignKey("ReplayId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();

                    b.Navigation("Replay");
                });

            modelBuilder.Entity("ReplayBrowser.Data.Models.ReplayParticipant", b =>
                {
                    b.HasOne("ReplayBrowser.Data.Models.Replay", "Replay")
                        .WithMany("RoundParticipants")
                        .HasForeignKey("ReplayId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();

                    b.Navigation("Replay");
                });

            modelBuilder.Entity("ReplayBrowser.Data.Models.Account.Account", b =>
                {
                    b.Navigation("History");

                    b.Navigation("Webhooks");
                });

            modelBuilder.Entity("ReplayBrowser.Data.Models.Account.Webhook", b =>
                {
                    b.Navigation("Logs");
                });

            modelBuilder.Entity("ReplayBrowser.Data.Models.CollectedPlayerData", b =>
                {
                    b.Navigation("Characters");

                    b.Navigation("JobCount");
                });

            modelBuilder.Entity("ReplayBrowser.Data.Models.Replay", b =>
                {
                    b.Navigation("Events");

                    b.Navigation("RoundParticipants");
                });

            modelBuilder.Entity("ReplayBrowser.Data.Models.ReplayParticipant", b =>
                {
                    b.Navigation("Players");
                });
#pragma warning restore 612, 618
        }
    }
}
