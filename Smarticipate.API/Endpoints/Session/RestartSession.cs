using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Smarticipate.API.Data.Identity;
using Smarticipate.API.Hubs;
using Smarticipate.API.Services;

namespace Smarticipate.API.Endpoints.Session;

public class RestartSession : IEndpoint
{
    public static void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPut("api/sessions/{sessionCode}/restart", Handle)
            .RequireAuthorization()
            .WithTags("Sessions")
            .WithName("Restart Session")
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
        if (string.IsNullOrEmpty(userId)) return Results.Unauthorized();

        var session = await db.Sessions
            .OrderByDescending(s => s.StartTime)
            .FirstOrDefaultAsync(s => s.SessionCode == sessionCode);

        if (session is null || session.UserId != userId) return Results.NotFound();

        // Enforce one active session per teacher: close any currently-open ones first. Without this, GetActiveSession (orders by StartTime desc) could load the wrong one.
        var others = await db.Sessions
            .Where(s => s.UserId == session.UserId && s.EndTime == null && s.Id != session.Id)
            .ToListAsync();
        foreach (var o in others)
            o.EndTime = DateTime.Now;

        session.EndTime = null;             // active again
        session.StartTime = DateTime.Now;   // bump so it sorts to the top in GetActiveSession
        await db.SaveChangesAsync();

        // Release the live state of the sessions we just auto-closed: their students
        // are still connected and must be told the session ended.
        foreach (var o in others)
        {
            await LiveSessionTerminator.EndAsync(db, feedbackStore, hub, o.SessionCode);
        }

        return Results.NoContent();
    }
}