using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Smarticipate.API.Data.Identity;
using Smarticipate.API.Hubs;
using Smarticipate.API.Services;

namespace Smarticipate.API.Endpoints.Session;

// Hard-deletes a session. FK relationships cascade, so its feedback snapshots, questions, and responses go with it. Owner-only.
public class DeleteSession : IEndpoint
{
    public static void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapDelete("api/sessions/{sessionCode}", Handle)
            .RequireAuthorization()
            .WithTags("Sessions")
            .WithName("Delete Session")
            .Produces(StatusCodes.Status204NoContent)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status404NotFound);
    }

    private static async Task<IResult> Handle(
        string sessionCode,
        ClaimsPrincipal user,
        [FromServices] UserDbContext db,
        [FromServices] LiveFeedbackStore feedbackStore,
        [FromServices] IHubContext<SessionHub> hub)
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

        // If it's still live, release connected students and clear the in-memory store before the row (and its now-orphaned questions) disappear.
        if (session.EndTime == null)
            await LiveSessionTerminator.EndAsync(db, feedbackStore, hub, sessionCode);

        db.Sessions.Remove(session);
        await db.SaveChangesAsync();
        return Results.NoContent();
    }
}
