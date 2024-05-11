using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authentication;

namespace Client.Controllers;

[Controller]
[Route("/account/")]
public class Account : Controller
{
    [Route("login")]
    public IActionResult Login()
    {
        return Challenge(new AuthenticationProperties
        {
            RedirectUri = "/"
        });
    }
    
    [Route("logout")]
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync("Cookies");
        return Redirect("/");
    }
}