using System.Net;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.EntityFrameworkCore;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;
using Serilog;
using Serilog.AspNetCore;
using Server;
using Server.Api;
using Server.Metrics;
using Server.ReplayParser;

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
    
    builder.Services.AddSingleton<ReplayMetrics>();
    
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
    
    var app = builder.Build();

    var webSocketOptions = new WebSocketOptions()
    {
        KeepAliveInterval = TimeSpan.FromSeconds(10),
    };
    
    app.UseWebSockets(webSocketOptions);
    
    // Configure the HTTP request pipeline.
    if (app.Environment.IsDevelopment())
    {
        app.UseSwagger();
        app.UseSwaggerUI();
    }
    
    app.UseHttpsRedirection();
    
    app.UseRouting();
    app.UseCors();
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