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
        services.AddSingleton<AnalyticsService>();
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

        services.AddOpenTelemetry().WithMetrics(providerBuilder =>
        {
            providerBuilder.AddPrometheusExporter();


            providerBuilder.AddMeter("Microsoft.AspNetCore.Hosting",
                "Microsoft.AspNetCore.Server.Kestrel");

            providerBuilder.AddAspNetCoreInstrumentation()
                .AddHttpClientInstrumentation()
                .AddRuntimeInstrumentation()
                .AddProcessInstrumentation();

            providerBuilder.AddMeter(
                "Microsoft.Extensions.Diagnostics.ResourceMonitoring",
                "Microsoft.AspNetCore.Routing",
                "Microsoft.AspNetCore.Diagnostics",
                "System.Net.Http",
                "ReplayBrowser");

            providerBuilder.AddView("http.server.request.duration",
                new ExplicitBucketHistogramConfiguration
                {
                    Boundaries = new double[]
                    {
                        0, 0.005, 0.01, 0.025, 0.05,
                        0.075, 0.1, 0.25, 0.5, 0.75, 1, 2.5, 5, 7.5, 10
                    }
                });

            providerBuilder.AddView("http.server.request.size",
                new ExplicitBucketHistogramConfiguration
                {
                    Boundaries = new double[]
                    {
                        0, 100, 1024, 1024 * 10, 1024 * 100, 1024 * 1024, 1024 * 1024 * 10, 1024 * 1024 * 100
                    }
                });

            providerBuilder.AddView("http.server.response.size",
                new ExplicitBucketHistogramConfiguration
                {
                    Boundaries = new double[]
                    {
                        0, 100, 1024, 1024 * 10, 1024 * 100, 1024 * 1024, 1024 * 1024 * 10, 1024 * 1024 * 100
                    }
                });

            providerBuilder.AddView("http.server.response.duration",
                new ExplicitBucketHistogramConfiguration
                {
                    Boundaries = new double[]
                    {
                        0, 0.005, 0.01, 0.025, 0.05,
                        0.075, 0.1, 0.25, 0.5, 0.75, 1, 2.5, 5, 7.5, 10
                    }
                });

            providerBuilder.AddView("http.server.response.status",
                new ExplicitBucketHistogramConfiguration
                {
                    Boundaries = new double[]
                    {
                        0, 100, 200, 300, 400, 500
                    }
                });
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

            options.Authority = "https://central.spacestation14.io/web/";
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

        app.Use((context, next) =>
        {
            context.Request.Scheme = "https";
            return next();
        });

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

        app.UseStaticFiles();

        app.UseRouting();

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
            endpoints.MapPrometheusScrapingEndpoint();
            endpoints.MapControllerRoute(
                name: "default",
                pattern: "api/{controller=Home}/{action=Index}/{id?}");
        });

        app.UseHttpsRedirection();
        app.UseForwardedHeaders();
        app.UseRouting();
        app.UseCors();
    }
}