using System.Diagnostics;
using System.Security.Claims;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using ReplayBrowser.Data;
using ReplayBrowser.Data.Models.Account;
using ReplayBrowser.Helpers;
using ReplayBrowser.Models;
using Serilog;

namespace ReplayBrowser.Services;

/// <summary>
/// This service is responsible for getting account settings. These are pregenerated and stored in a memory cache.
/// </summary>
public class AccountService : IHostedService, IDisposable
{
    private readonly IMemoryCache _cache;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly Ss14ApiHelper _apiHelper;

    private bool _settingsGenerated = false;
    private Timer? _timer = null;

    public AccountService(IMemoryCache cache, IServiceScopeFactory scopeFactory, Ss14ApiHelper apiHelper)
    {
        _cache = cache;
        _scopeFactory = scopeFactory;
        _apiHelper = apiHelper;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        GenerateAccountSettings();
        _timer = new Timer(CheckAccounts, null, TimeSpan.Zero, TimeSpan.FromMinutes(1));
        return Task.CompletedTask;
    }

    private async void CheckAccounts(object? state)
    {
        using var scope = _scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ReplayDbContext>();

        CheckDuplicate(context);
        await CheckApiErrorName(context);

        await context.SaveChangesAsync();
    }

    /// <summary>
    /// This method checks for accounts with the name "Unable to fetch username (API error)" tries to fetch the username again.
    /// </summary>
    private async Task CheckApiErrorName(ReplayDbContext context)
    {
        var accounts = context.Accounts
            .Include(a => a.Settings)
            .Include(a => a.History)
            .ToList();

        foreach (var account in accounts)
        {
            if (account.Username == "Unable to fetch username (API error)")
            {
                Log.Warning($"Account {account.Guid} has an error name. Trying to fetch username again.");
                var playerData = await _apiHelper.FetchPlayerDataFromGuid(account.Guid);
                account.Username = playerData?.Username ?? "Unable to fetch username (API error)";
            }
        }
    }

    /// <summary>
    /// This method checks for duplicate accounts in the database and removes them. Why are there duplicates? I'm not sure. I fucked up *somewhere*
    /// </summary>
    private void CheckDuplicate(ReplayDbContext context)
    {
        var accounts = context.Accounts
            .Include(a => a.Settings)
            .Include(a => a.History)
            .ToList();

        var duplicateAccounts = accounts
            .GroupBy(a => a.Guid)
            .Where(g => g.Count() > 1);

        foreach (var group in duplicateAccounts)
        {
            var accountToRemove = group.OrderByDescending(a => a.Id).First();
            Log.Warning($"Duplicate account found: {accountToRemove.Username} ({accountToRemove.Guid})");
            context.Accounts.Remove(accountToRemove);
        }
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _timer?.Change(Timeout.Infinite, 0);
        return Task.CompletedTask;
    }

    /// <summary>
    /// Get the account settings for a given account.
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown if the account settings have not been generated yet.</exception>
    public AccountSettings? GetAccountSettings(Guid accountGuid)
    {
        if (!_settingsGenerated)
        {
            throw new InvalidOperationException("Account settings have not been generated yet.");
        }

        return _cache.Get<AccountSettings?>($"Account_{accountGuid}")!;
    }

    public void GenerateAccountSettings()
    {
        var stopwatch = new Stopwatch();
        stopwatch.Start();
        Log.Information("Generating account settings...");

        using var scope = _scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ReplayDbContext>();
        var accounts = context.Accounts
            .Include(a => a.Settings)
            .ToList();

        foreach (var account in accounts)
        {
            _cache.Set($"Account_{account.Guid}", account.Settings);
        }

        stopwatch.Stop();
        Log.Information($"Generated account settings in {stopwatch.ElapsedMilliseconds}ms.");
        _settingsGenerated = true;
    }

    public void Dispose()
    {
        _cache.Dispose();
        _timer?.Dispose();
    }

    public async Task<Account?> GetAccount(AuthenticationState authstate, bool includeHistory = false)
    {
        var guid = AccountHelper.GetAccountGuid(authstate);
        using var scope = _scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ReplayDbContext>();
        Account? account = null;
        if (includeHistory)
        {
            account = context.Accounts
                .Include(a => a.Settings)
                .Include(a => a.History)
                //.Include(a => a.Webhooks)
                .FirstOrDefault(a => a.Guid == guid);
        } else {
            account = context.Accounts
                .Include(a => a.Settings)
                //.Include(a => a.Webhooks)
                .FirstOrDefault(a => a.Guid == guid);
        }

        if (account == null)
        {
            if (guid == null)
            {
                return null;
            }

            // If the account doesn't exist, we need to create it.
            account = new Account()
            {
                Guid = (Guid)guid,
                Username = (await _apiHelper.FetchPlayerDataFromGuid((Guid)guid)).Username,
                Settings = new AccountSettings()
            };

            context.Accounts.Add(account);
            await context.SaveChangesAsync();
        }

        return account;
    }

    public async Task UpdateAccount(Account? account)
    {
        if (account == null)
        {
            return;
        }

        using var scope = _scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ReplayDbContext>();
        context.Accounts.Update(account);
        GenerateAccountSettings();
        await context.SaveChangesAsync();
    }

    public async Task<AccountHistoryResponse?> GetAccountHistory(string username, int pageNumber)
    {
        using var scope = _scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ReplayDbContext>();

        var historyQuery = context.Set<HistoryEntry>().AsQueryable();

        if (string.IsNullOrWhiteSpace(username))
            historyQuery = historyQuery.Where(h => h.Account.Guid == Guid.Empty);
        else
            historyQuery = historyQuery.Where(h => h.Account.Username == username);

        var totalCount = await historyQuery.CountAsync();

        var history = await historyQuery
            .OrderByDescending(h => h.Time)
            .Skip(pageNumber * 10)
            .Take(10)
            .ToListAsync();

        return new AccountHistoryResponse
        {
            History = history,
            Page = pageNumber,
            TotalPages = totalCount / 10
        };
    }

    private Account GetSystemAccount()
    {
        using var scope = _scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ReplayDbContext>();
        return context.Accounts
            .Include(a => a.Settings)
            .First(a => a.Guid == Guid.Empty);
    }

    public async Task AddHistory(Account? callerAccount, HistoryEntry historyEntry)
    {
        callerAccount ??= GetSystemAccount();

        callerAccount.History.Add(historyEntry);
        await UpdateAccount(callerAccount);
    }

    /// <summary>
    /// Returns all accounts and their settings
    /// </summary>
    public async Task<List<Account>> GetAllAccounts()
    {
        using var scope = _scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ReplayDbContext>();
        return await context.Accounts
            .Include(a => a.Settings)
            .ToListAsync();
    }

    /// <summary>
    /// Deletes an account from the database, including logs and settings.
    /// </summary>
    public async Task DeleteAccount(Account account)
    {
        using var scope = _scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ReplayDbContext>();
        context.Accounts.Remove(account);
        await context.SaveChangesAsync();
        Log.Information($"Deleted account {account.Username} ({account.Guid})");
    }

    public bool IsAdmin(ClaimsPrincipal user)
    {
        // Find account.
        var guid = AccountHelper.GetAccountGuid(user);
        if (guid == null)
        {
            return false;
        }

        using var scope = _scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ReplayDbContext>();
        return context.Accounts.SingleOrDefault(a => a.Guid == guid)?.IsAdmin ?? false;
    }
}