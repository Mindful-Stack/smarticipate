using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Smarticipate.API.Data.Identity;

namespace Smarticipate.API.Endpoints.Session;

// Sets (or changes) a session's display name. The session code is never touched. Owner-only.
public class RenameSession : IEndpoint
{
    public static void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPut("api/sessions/{sessionCode}/name", Handle)
            .RequireAuthorization()
            .WithTags("Sessions")
            .WithName("Rename Session")
            .Accepts<Request>("application/json")
            .Produces(StatusCodes.Status204NoContent)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status404NotFound);
    }

    public record Request([FromBody] string? Name);

    private static async Task<IResult> Handle(
        string sessionCode,
        Request request,
        ClaimsPrincipal user,
        [FromServices] UserDbContext db)
    {
        var userId = user.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(userId))
            return Results.Unauthorized();

        var session = await db.Sessions
            .OrderByDescending(s => s.StartTime)
            .FirstOrDefaultAsync(s => s.SessionCode == sessionCode);

        // Treat "not yours" the same as "doesn't exist" so we don't leak other users' codes.
        if (session is null || session.UserId != userId)
            return Results.NotFound();

        // Trim, and treat blank as "no name" (null) so the row falls back to the code.
        var name = request.Name?.Trim();
        session.Name = string.IsNullOrWhiteSpace(name) ? null : name;
        await db.SaveChangesAsync();
        return Results.NoContent();
    }
}
