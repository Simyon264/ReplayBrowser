using System.Diagnostics;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using ReplayBrowser.Data;
using ReplayBrowser.Data.Models.Account;
using ReplayBrowser.Helpers;
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
    
    public AccountService(IMemoryCache cache, IServiceScopeFactory scopeFactory, Ss14ApiHelper apiHelper)
    {
        _cache = cache;
        _scopeFactory = scopeFactory;
        _apiHelper = apiHelper;
    }
    
    public Task StartAsync(CancellationToken cancellationToken)
    {
        GenerateAccountSettings();
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
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
    }

    public async Task<Account>? GetAccount(AuthenticationState authstate)
    {
        var guid = AccountHelper.GetAccountGuid(authstate);
        using var scope = _scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ReplayDbContext>();
        var account = context.Accounts
            .Include(a => a.Settings)
            .Include(a => a.History)
            .FirstOrDefault(a => a.Guid == guid);
        
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
        await context.SaveChangesAsync();
    }

    public async Task<AccountHistoryResponse?> GetAccountHistory(string username, int pageNumber)
    {
        // If the username is empty, we want to see logs for not logged in users.
        if (string.IsNullOrWhiteSpace(username))
        {
            var systemAccount = GetSystemAccount();
            
            systemAccount.History = systemAccount.History.OrderByDescending(h => h.Time).ToList();
        
            AccountHistoryResponse response = new()
            {
                History = [],
                Page = pageNumber,
                TotalPages = 1
            };
        
            if (systemAccount.History.Count > 10)
            {
                response.TotalPages = systemAccount.History.Count / 10;
            }
        
            response.History = systemAccount.History.Skip(pageNumber * 10).Take(10).ToList();
            return response;
        } else {
            using var scope = _scopeFactory.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<ReplayDbContext>();
            var account = context.Accounts
                .Include(a => a.History)
                .FirstOrDefault(a => a.Username == username);
            
            if (account == null)
            {
                return null;
            }
            
            account.History = account.History.OrderByDescending(h => h.Time).ToList();
        
            AccountHistoryResponse response = new()
            {
                History = [],
                Page = pageNumber,
                TotalPages = 1
            };
        
            if (account.History.Count > 10)
            {
                response.TotalPages = account.History.Count / 10;
            }
        
            response.History = account.History.Skip(pageNumber * 10).Take(10).ToList();
            return response;
        }
    }
    
    private Account GetSystemAccount()
    {
        using var scope = _scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ReplayDbContext>();
        return context.Accounts
            .Include(a => a.Settings)
            .Include(a => a.History)
            .First(a => a.Guid == Guid.Empty);
    }

    public async Task AddHistory(Account? callerAccount, HistoryEntry historyEntry)
    {
        if (callerAccount == null)
        {
            var systemAccount = GetSystemAccount();
            systemAccount.History.Add(historyEntry);
            await UpdateAccount(systemAccount);
        } else {
            callerAccount.History.Add(historyEntry);
            await UpdateAccount(callerAccount);
        }
    }
}