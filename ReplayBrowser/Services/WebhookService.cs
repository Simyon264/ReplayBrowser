using System.Text;
using System.Text.Json;
using Discord;
using Discord.Webhook;
using Microsoft.EntityFrameworkCore;
using ReplayBrowser.Data;
using ReplayBrowser.Data.Models;
using ReplayBrowser.Data.Models.Account;
using Serilog;
using WebhookType = ReplayBrowser.Data.Models.Account.WebhookType;

namespace ReplayBrowser.Services;

public class WebhookService
{
    private IServiceScopeFactory _scopeFactory;

    public WebhookService(IServiceScopeFactory scopeFactory)
    {
        _scopeFactory = scopeFactory;
    }

    public async Task SendReplayToWebhooks(Replay parsedReplay)
    {
        using var scope = _scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ReplayDbContext>();

        var accountsWithWebhooks = context.Accounts
            .Include(a => a.Webhooks)
            .Where(a => a.Webhooks.Count != 0)
            .ToList();

        foreach (var account in accountsWithWebhooks)
        {
            foreach (var webhook in account.Webhooks)
            {
                var servers = webhook.Servers.Split(',');
                if (!servers.Contains(parsedReplay.ServerName) && !string.IsNullOrWhiteSpace(webhook.Servers))
                {
                    continue;
                }

                if (webhook.Type == WebhookType.Discord)
                {
                    await SendDiscordWebhook(webhook, parsedReplay);
                }
                else if (webhook.Type == WebhookType.Json)
                {
                    await SendJsonWebhook(webhook, parsedReplay);
                }
            }
        }
    }

    private async Task SendDiscordWebhook(Webhook webhook, Replay parsedReplay)
    {
        using var scope = _scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ReplayDbContext>();
        var configuration = scope.ServiceProvider.GetRequiredService<IConfiguration>();

        var webhookClient = new DiscordWebhookClient(webhook.Url);
        var description = new StringBuilder();
        if (parsedReplay.RoundParticipants != null)
        {
            description.AppendLine($"**Players:** {parsedReplay.RoundParticipants.Count}");
        }
        description.AppendLine($"**Duration:** {parsedReplay.Duration}");
        description.AppendLine($"**Mode:** {parsedReplay.Gamemode}");
        if (parsedReplay.Map == null && parsedReplay.Maps != null)
        {
            description.AppendLine($"**Maps:** {string.Join(", ", parsedReplay.Maps)}");
        }
        else
        {
            description.AppendLine($"**Map:** {parsedReplay.Map}");
        }

        var embed = new EmbedBuilder()
        {
            Url = $"{configuration["RedirectUri"]}replay/{parsedReplay.Id}",
            Title = $"New replay parsed: {parsedReplay.RoundId} - {parsedReplay.ServerName} ({parsedReplay.ServerId})",
            Description = description.ToString(),
            Color = Color.Green,
            Timestamp = DateTime.UtcNow
        };

        try
        {
            await webhookClient.SendMessageAsync(
                embeds: new[] { embed.Build() },
                allowedMentions: AllowedMentions.None,
                avatarUrl: configuration["RedirectUri"] + "assets/img/replaybrowser-white.png"
            );
        }
        catch (Exception e)
        {
            var logFailed = new WebhookHistory
            {
                SentAt = DateTime.UtcNow,
                ResponseCode = 0,
                ResponseBody = e.Message,
                WebhookId = webhook.Id
            };

            webhook.Logs.Add(logFailed);
            await context.SaveChangesAsync();
            Log.Error(e, "Failed to send Discord webhook to {WebhookUrl}.", webhook.Url);
        }

        var log = new WebhookHistory
        {
            SentAt = DateTime.UtcNow,
            ResponseCode = 200,
            ResponseBody = "Success",
            WebhookId = webhook.Id
        };

        webhook.Logs.Add(log);
        await context.SaveChangesAsync();
    }

    private async Task SendJsonWebhook(Webhook webhook, Replay parsedReplay)
    {
        using var scope = _scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ReplayDbContext>();

        var client = new HttpClient();
        var content = new StringContent(JsonSerializer.Serialize(parsedReplay), Encoding.UTF8, "application/json");

        HttpResponseMessage response;
        try
        {
            response = await client.PostAsync(webhook.Url, content);
        }
        catch (Exception e)
        {
            var logFailed = new WebhookHistory
            {
                SentAt = DateTime.UtcNow,
                ResponseCode = 0,
                ResponseBody = e.Message,
                WebhookId = webhook.Id
            };

            webhook.Logs.Add(logFailed);
            await context.SaveChangesAsync();
            Log.Error(e, "Failed to send JSON webhook to {WebhookUrl}.", webhook.Url);
            return;
        }

        var log = new WebhookHistory
        {
            SentAt = DateTime.UtcNow,
            ResponseCode = (int)response.StatusCode,
            ResponseBody = await response.Content.ReadAsStringAsync(),
            WebhookId = webhook.Id
        };

        webhook.Logs.Add(log);
        await context.SaveChangesAsync();
    }
}