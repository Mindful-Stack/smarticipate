using System.Security.Claims;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Smarticipate.API.Data.Identity;
using Smarticipate.API.Hubs;
using Smarticipate.API.Services;

namespace Smarticipate.API.Endpoints.Session;

public class CreateSession : IEndpoint
{
    public static void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPost("api/sessions", Handle)
            .RequireAuthorization()
            .WithTags("Sessions")
            .WithName("Create Session")
            .Accepts<Request>("application/json")
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces<Response>(StatusCodes.Status201Created);
    }

    public record Request(
        [FromBody] string SessionCode,
        [FromBody] string? Name,
        [FromBody] DateTime? StartTime,
        [FromBody] DateTime? EndTime,
        [FromBody] string UserId
    );

    public record Response(
        int Id
    );

    // Return type widens to IResult so we can return Unauthorized as well as Created.
    private static async Task<IResult> Handle(
        Request request,
        ClaimsPrincipal user,
        [FromServices] UserDbContext db,
        [FromServices] LiveFeedbackStore feedbackStore,
        [FromServices] IHubContext<SessionHub> hub)
    {
        var userId = user.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(userId))
            return Results.Unauthorized();

        // Enforce one active session per teacher: close any currently-open ones first so GetActive (orders by StartTime desc) is unambiguous
        var openSessions = await db.Sessions
            .Where(s => s.UserId == userId && s.EndTime == null)
            .ToListAsync();
        foreach (var s in openSessions)
        {
            s.EndTime = DateTime.Now;
        }

        var newSession = new Core.Entities.Session
        {
            SessionCode = request.SessionCode,
            Name = request.Name,
            StartTime = request.StartTime,
            EndTime = request.EndTime,
            UserId = userId    // authenticated caller, NOT the body
        };

        db.Sessions.Add(newSession);
        await db.SaveChangesAsync();

        // Release the live state of the sessions we just auto-closed: their students
        // are still connected and must be told the session ended.
        foreach (var s in openSessions)
        {
            await LiveSessionTerminator.EndAsync(db, feedbackStore, hub, s.SessionCode);
        }

        return TypedResults.Created($"api/sessions/{newSession.Id}", new Response(newSession.Id));
    }
}