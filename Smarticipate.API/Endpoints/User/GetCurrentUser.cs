using System.Security.Claims;

namespace Smarticipate.API.Endpoints.User;

// Returns the authenticated caller's own user id, nothing to enumerate.
public class GetCurrentUser : IEndpoint
{
    public static void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapGet("api/users/me", (ClaimsPrincipal user) =>
            {
                var id = user.FindFirstValue(ClaimTypes.NameIdentifier);
                return string.IsNullOrEmpty(id) ? Results.Unauthorized() : Results.Ok(id);
            })
            .RequireAuthorization()
            .WithTags("Users")
            .WithName("Get Current User")
            .Produces<string>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status401Unauthorized);
    }
}
