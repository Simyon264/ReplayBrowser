using Microsoft.AspNetCore.Authorization;

namespace Server.Auth;

public class TokenRequirement : IAuthorizationRequirement
{
    public string RequiredToken { get; }

    public TokenRequirement(string requiredToken)
    {
        RequiredToken = requiredToken;
    }
}