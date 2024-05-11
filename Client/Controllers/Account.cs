using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authentication;

namespace Client.Controllers;

[Controller]
[Route("/account/")]
public class Account : Controller
{
    private readonly IConfiguration _configuration;
    
    public Account(IConfiguration configuration)
    {
        _configuration = configuration;
    }
    
    [Route("login")]
    public IActionResult Login()
    {
        return Challenge(new AuthenticationProperties
        {
            RedirectUri = _configuration["RedirectUri"]
        });
    }
    
    [Route("logout")]
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync("Cookies");
        return Redirect(_configuration["RedirectUri"]);
    }
}