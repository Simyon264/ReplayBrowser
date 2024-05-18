using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc;

namespace ReplayBrowser.Controllers;

[Controller]
[Route("/account/")]
public class AccountController : Controller
{
    private readonly IConfiguration _configuration;
    
    public AccountController(IConfiguration configuration)
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
        return Redirect("/");
    }
}