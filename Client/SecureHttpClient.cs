using System.Net.Http.Headers;
using Microsoft.AspNetCore.Components.Authorization;

namespace Client;

public class SecureHttpClient
{
    private readonly IConfiguration _configuration;
    
    public SecureHttpClient(IConfiguration configuration)
    {
        _configuration = configuration;
        
    }
    
    public HttpClient GetClient(AuthenticationState authState)
    {
        var httpClient = new HttpClient { BaseAddress = new Uri(_configuration["ApiUrl"]) };
        var token = _configuration["Token"];
        if (token == null)
        {
            throw new Exception("Token is null, please set the token in the configuration.");
        }
        
        httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _configuration["Token"]);
        if (authState.User.Identity.IsAuthenticated)
        {
            httpClient.DefaultRequestHeaders.Add("accountGuid", authState.User.Claims.First(c => c.Type == "http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier").Value);
        }
        
        return httpClient;
    }
}