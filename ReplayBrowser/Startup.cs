using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using OpenTelemetry.Metrics;
using ReplayBrowser.Data;
using ReplayBrowser.Data.Models.Account;
using ReplayBrowser.Helpers;
using ReplayBrowser.Pages.Shared;
using ReplayBrowser.Pages.Shared.Layout;
using ReplayBrowser.Services;
using ReplayBrowser.Services.ReplayParser;
using Serilog;
using Serilog.AspNetCore;

namespace ReplayBrowser;

public class Startup
{
    public Startup(IConfiguration configuration)
    {
        Configuration = configuration;
    }

    public IConfiguration Configuration { get; }

    public void ConfigureServices(IServiceCollection services)
    {
        services.AddMemoryCache(opts =>
        {
            opts.TrackStatistics = true;
        });

        services.AddControllersWithViews().AddJsonOptions(options =>
        {
            options.JsonSerializerOptions.ReferenceHandler = ReferenceHandler.IgnoreCycles;
        });

        services.AddDbContext<ReplayDbContext>(options =>
        {
            if (Configuration.GetConnectionString("DefaultConnection") == null)
            {
                Log.Fatal("No connection string found in appsettings.json. Exiting.");
                Environment.Exit(1);
            }

            // FIXME: Timeout needs to bumped down to default after the big migration finishes
            options.UseNpgsql(Configuration.GetConnectionString("DefaultConnection"), o => o.CommandTimeout(600));

            options.EnableSensitiveDataLogging();
        });

        services.AddSingleton<Ss14ApiHelper>();
        services.AddSingleton<AccountService>();
        services.AddSingleton<LeaderboardService>();
        services.AddSingleton<ReplayParserService>();
        services.AddSingleton<NoticeHelper>();
        services.AddSingleton<GitHubApiHelper>();

        services.AddScoped<ReplayHelper>();

        services.AddCors(options =>
        {
            options.AddDefaultPolicy(builder =>
            {
                builder.AllowAnyOrigin()
                    .AllowAnyMethod()
                    .AllowAnyHeader();
            });
        });

        // Endpoint logging
        services.Configure<RequestLoggingOptions>(options =>
        {
            options.MessageTemplate = "Handled {RequestPath}";
        });

        services.Configure<ForwardedHeadersOptions>(options =>
        {
            options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
            var proxyIP = Configuration["ProxyIP"];
            if (proxyIP == null)
            {
                proxyIP = "127.0.10.1";
                Log.Warning("No ProxyIP set in configuration. Defaulting to {ProxyIP}", proxyIP);
            }

            Log.Information("Proxy IP: {ProxyIP}", proxyIP);
            options.KnownProxies.Add(IPAddress.Parse(proxyIP));
        });

        services.AddRazorComponents()
            .AddInteractiveServerComponents();

        services.AddHttpContextAccessor();

        JwtSecurityTokenHandler.DefaultInboundClaimTypeMap.Clear();

        #if !TESTING
        services.AddAuthentication(options =>
        {
            options.DefaultScheme = "Cookies";
            options.DefaultChallengeScheme = "oidc";
        }).AddCookie("Cookies", options =>
        {
            options.ExpireTimeSpan = TimeSpan.FromDays(30);
        }).AddOpenIdConnect("oidc", options =>
        {
            options.SignInScheme = "Cookies";

            options.Authority = "https://account.spacestation14.com/";
            options.ClientId = Configuration["ClientId"];
            options.ClientSecret = Configuration["ClientSecret"];

            options.ResponseType = OpenIdConnectResponseType.Code;
            options.RequireHttpsMetadata = false;

            options.Scope.Add("openid");
            options.Scope.Add("profile");

            options.GetClaimsFromUserInfoEndpoint = true;
        });
        #endif


        services.AddHttpLogging(o => { });

        services.AddSwaggerGen();
    }

    public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
    {
        // Run migrations on startup.
        {
            using var dbScope = app.ApplicationServices.CreateScope();
            using var replayContext = dbScope.ServiceProvider.GetRequiredService<ReplayDbContext>();
            replayContext!.Database.Migrate();

            // We need to create a "dummy" system Account to use for unauthenticated requests.
            // This is because the Account system is used for logging and we need to have an account to log as.
            // This is a bit of a hack but it works.
            try
            {
                var systemAccount = new Account()
                {
                    Guid = Guid.Empty,
                    Username = "[System] Unauthenticated user",
                };

                if (replayContext.Accounts.FirstOrDefault(a => a.Guid == Guid.Empty) == null) // Only add if it doesn't already exist.
                {
                    replayContext.Accounts.Add(systemAccount);
                    replayContext.SaveChanges();
                }
            }
            catch (Exception e)
            {
                Log.Error(e, "Failed to create system account.");
            }
        }

#if RELEASE
        app.UseExceptionHandler("/error", createScopeForErrors: true);
        app.UseHsts();
#else
        app.UseDeveloperExceptionPage();
#endif
        app.UseStatusCodePagesWithReExecute("/error/{0}");

#if !TESTING
        app.UseHttpsRedirection();
#endif

        app.UseForwardedHeaders();
        app.UseRouting();
        app.UseCors();

        app.UseStaticFiles();

        app.UseAuthentication();
        app.UseAuthorization();
        app.UseAntiforgery();

        app.UseSwagger();
        app.UseSwaggerUI();

        app.UseEndpoints(endpoints =>
        {
            endpoints.MapSwagger();
            endpoints.MapControllers();
            endpoints.MapRazorComponents<App>()
                .AddInteractiveServerRenderMode();
            endpoints.MapControllerRoute(
                name: "default",
                pattern: "api/{controller=Home}/{action=Index}/{id?}");
        });
    }
}