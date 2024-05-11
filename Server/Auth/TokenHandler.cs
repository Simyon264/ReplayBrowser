using Microsoft.AspNetCore.Authorization;

namespace Server.Auth;

public class TokenHandler : AuthorizationHandler<TokenRequirement>
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public TokenHandler(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    protected override Task HandleRequirementAsync(AuthorizationHandlerContext context, TokenRequirement requirement)
    {
        var authHeader = _httpContextAccessor.HttpContext.Request.Headers["Authorization"].FirstOrDefault();

        if (authHeader != null && authHeader == $"Bearer {requirement.RequiredToken}")
        {
            context.Succeed(requirement);
        }

        return Task.CompletedTask;
    }
}