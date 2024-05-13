using System.Net;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;
using Serilog;
using Serilog.AspNetCore;
using Server;
using Server.Api;
using Server.Auth;
using Server.Helpers;
using Server.Metrics;
using Server.ReplayParser;
using Shared.Models.Account;

Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .WriteTo.File("logs/log.txt", rollingInterval: RollingInterval.Day, fileSizeLimitBytes: null)
    .CreateLogger();

try
{
    var builder = WebApplication.CreateBuilder(args);
    
    builder.Host.UseSerilog();
    
    builder.WebHost.UseKestrel();
    builder.WebHost.ConfigureKestrel(options => options.Limits.MaxRequestBodySize = long.MaxValue);
    
    // Add services to the container.
    // Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
    builder.Services.AddEndpointsApiExplorer();
    builder.Services.AddSwaggerGen();
    
    // Load in appsettings.json, and appsettings.Development.json if the environment is development.
    // Additionally, load in appsettings.Secret.json if it exists.
    builder.Configuration.AddJsonFile("appsettings.json", optional: false, reloadOnChange: true);
    builder.Configuration.AddJsonFile($"appsettings.{builder.Environment.EnvironmentName}.json", optional: true, reloadOnChange: true);
    builder.Configuration.AddJsonFile("appsettings.Secret.json", optional: true, reloadOnChange: true);
    
    builder.Services.AddControllersWithViews().AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.ReferenceHandler = ReferenceHandler.IgnoreCycles;
    });
    builder.Services.AddDbContext<ReplayDbContext>(options =>
    {
        options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection"));
    });
    
    builder.Services.AddDbContext<ReplayParserDbContext>(options =>
    {
        options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection"));
    });
    
    // Run migrations on startup.
    var replayContext = builder.Services.BuildServiceProvider().GetService<ReplayDbContext>();
    replayContext.Database.Migrate();
        
    // We need to create a "dummy" system Account to use for unauthenticated requests.
    // This is because the Account system is used for logging and we need to have an account to log as.
    // This is a bit of a hack but it works.
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
    
    builder.Services.AddSingleton<ReplayMetrics>();
    builder.Services.AddSingleton<Ss14ApiHelper>();
    
    ReplayParser.Context = builder.Services.BuildServiceProvider().GetService<ReplayParserDbContext>(); // GOD THIS IS STUPID
    ReplayParser.Metrics = builder.Services.BuildServiceProvider().GetService<ReplayMetrics>();
    
    builder.Services.AddCors(options =>
    {
        options.AddDefaultPolicy(builder =>
        {
            builder.AllowAnyOrigin()
                .AllowAnyMethod()
                .AllowAnyHeader();
        });
    });
    
    // Endpoint logging
    builder.Services.Configure<RequestLoggingOptions>(options =>
    {
        options.MessageTemplate = "Handled {RequestPath}";
    });

    builder.Services.AddMemoryCache();
    
    builder.Services.AddMvc();
    
    // Proxy stuff
    builder.Services.Configure<ForwardedHeadersOptions>(options =>
    {
        options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
        options.KnownProxies.Add(IPAddress.Parse(builder.Configuration["ProxyIP"]));
    });
    
    builder.Services.AddOpenTelemetry().WithMetrics(providerBuilder =>
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
    
    builder.Services.AddHostedService<LeaderboardBackgroundService>();

    var authToken = builder.Configuration["Token"];
    if (string.IsNullOrWhiteSpace(authToken))
    {
        throw new Exception("No token found in appsettings.json. Please set Token to a valid and secure token.");
    }
    
    builder.Services.AddAuthorizationBuilder()
            .AddPolicy("TokenBased", policy =>
        {
            policy.Requirements.Add(new TokenRequirement(authToken));
        });
    
    builder.Services.AddSingleton<IAuthorizationHandler, TokenHandler>();
    builder.Services.AddSingleton<IHttpContextAccessor, HttpContextAccessor>();
    
    var app = builder.Build();
    
    // Configure the HTTP request pipeline.
    if (app.Environment.IsDevelopment())
    {
        app.UseSwagger();
        app.UseSwaggerUI();
    }
    
    app.UseHttpsRedirection();
    
    // Proxy stuff
    app.UseForwardedHeaders();
    
    app.UseRouting();
    app.UseCors();
    app.UseAuthentication();
    app.UseAuthorization();
    app.MapControllerRoute(
        name: "default",
        pattern: "api/{controller=Home}/{action=Index}/{id?}");
    
    // Run FetchReplays in a new thread.
    var tokens = new List<CancellationTokenSource>();
    var URLs = builder.Configuration.GetSection("ReplayUrls").Get<StorageUrl[]>();
    if (URLs == null)
    {
        throw new Exception("No replay URLs found in appsettings.json. Please set ReplayUrls to an array of URLs.");
    }
    
    var tokenSource = new CancellationTokenSource();
    tokens.Add(tokenSource);
    var thread = new Thread(() => ReplayParser.FetchReplays(tokenSource.Token, URLs));
    thread.Start();
    
    app.Lifetime.ApplicationStopping.Register(() =>
    {
        foreach (var token in tokens)
        {
            token.Cancel();
        }
    });
    app.MapPrometheusScrapingEndpoint();
    
    app.Run();
}
catch (Exception e)
{
    Log.Fatal(e, "An error occurred while starting the server.");
}
finally
{
    Log.CloseAndFlush();
}
