using System.IdentityModel.Tokens.Jwt;
using System.Net;
using Client;
using Client.Components;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.IdentityModel.Logging;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddScoped(sp => new HttpClient { BaseAddress = new Uri(builder.Configuration["ApiUrl"]) });
builder.Services.AddHttpContextAccessor();
builder.Services.AddSingleton<SecureHttpClient>();

builder.Configuration.AddJsonFile("appsettings.Secret.json", optional: true, reloadOnChange: true);

builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    options.KnownProxies.Add(IPAddress.Parse(builder.Configuration["ProxyIP"]));
});

JwtSecurityTokenHandler.DefaultInboundClaimTypeMap.Clear();

builder.Services.AddAuthentication(options =>
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
    options.ClientId = builder.Configuration["ClientId"];
    options.ClientSecret = builder.Configuration["ClientSecret"];
    
    options.ResponseType = OpenIdConnectResponseType.Code;
    options.RequireHttpsMetadata = false;
    
    options.Scope.Add("openid");
    options.Scope.Add("profile");
    
    options.GetClaimsFromUserInfoEndpoint = true;
});

builder.Services.AddControllersWithViews();
builder.Services.AddHttpLogging(o => { });

var app = builder.Build();

app.Use((context, next) =>
{
    context.Request.Scheme = "https";
    return next();
});

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();

app.UseStaticFiles();
app.UseAntiforgery();

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

IdentityModelEventSource.ShowPII = true;

app.Run();