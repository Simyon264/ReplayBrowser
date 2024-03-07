using System.Net;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.EntityFrameworkCore;
using Serilog;
using Serilog.AspNetCore;
using Server;
using Server.Api;

Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .WriteTo.File("logs/log.txt", rollingInterval: RollingInterval.Day, fileSizeLimitBytes: null)
    .CreateLogger();

try
{
    var builder = WebApplication.CreateBuilder(args);
    
    builder.Host.UseSerilog();
    
    builder.WebHost.UseKestrel();
    
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
    
    ReplayParser.Context = builder.Services.BuildServiceProvider().GetService<ReplayParserDbContext>(); // GOD THIS IS STUPID
    
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
    
    builder.Services.AddMvc();
    var app = builder.Build();
    
    // Configure the HTTP request pipeline.
    if (app.Environment.IsDevelopment())
    {
        app.UseSwagger();
        app.UseSwaggerUI();
    }
    
    app.UseHttpsRedirection();
    
    app.UseRouting();
    app.MapControllerRoute(
        name: "default",
        pattern: "api/{controller=Home}/{action=Index}/{id?}");
    
    // Run FetchReplays in a new thread.
    var tokens = new List<CancellationTokenSource>();
    var URLs = builder.Configuration.GetSection("ReplayUrls").Get<string[]>();
    if (URLs == null)
    {
        throw new Exception("No replay URLs found in appsettings.json. Please set ReplayUrls to an array of URLs.");
    }
    
    var tokenSource = new CancellationTokenSource();
    tokens.Add(tokenSource);
    var thread = new Thread(() => ReplayParser.FetchReplays(tokenSource.Token, URLs));
    thread.Start();
    
    var token = new CancellationTokenSource();
    tokens.Add(token);
    var thread2 = new Thread(() => ReplayParser.ConsumeQueue(token.Token));
    thread2.Start();
    
    app.Lifetime.ApplicationStopping.Register(() =>
    {
        foreach (var token in tokens)
        {
            token.Cancel();
        }
    });
    
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