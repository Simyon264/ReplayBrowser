using Microsoft.AspNetCore.Components.Authorization;

namespace ReplayBrowser.Helpers;

public static class AccountHelper
{
    public static Guid? GetAccountGuid(AuthenticationState authState)
    {
        if (authState.User.Identity == null)
        {
            return null;
        }
        
        if (!authState.User.Identity.IsAuthenticated)
        {
            return null;
        }
        
        return Guid.Parse(authState.User.Claims.First(c => c.Type == "http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier").Value);
    }
}