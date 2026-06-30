using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Smarticipate.API.Data.Identity;
using Smarticipate.API.Hubs;
using Smarticipate.API.Services;

namespace Smarticipate.API.Endpoints.Session;

public class UpdateSession : IEndpoint
{
    public static void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPut("api/sessions/{sessionCode}", Handle)
            .RequireAuthorization()
            .WithTags("Sessions")
            .WithName("Update Session")
            .Accepts<Request>("application/json")
            .Produces(StatusCodes.Status204NoContent)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status404NotFound);
    }

    public record Request(
        [FromBody] DateTime? EndTime
    );

    private static async Task<IResult> Handle(
        string sessionCode,
        Request request,
        ClaimsPrincipal user,
        [FromServices] UserDbContext db,
        [FromServices] LiveFeedbackStore feedbackStore,
        [FromServices] IHubContext<SessionHub> hub)
    {
        var userId = user.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(userId)) return Results.Unauthorized();

        var session = await db.Sessions.FirstOrDefaultAsync(s => s.SessionCode == sessionCode);

        if (session is null || session.UserId != userId)
        {
            return Results.NotFound();
        }

        session.EndTime = request.EndTime;
        await db.SaveChangesAsync();

        // Ending the session is server-authoritative: release live state so connected students are told it ended and nothing lingers in memory, no matter who stopped it (teacher page or dashboard).
        if (request.EndTime is not null)
            await LiveSessionTerminator.EndAsync(db, feedbackStore, hub, sessionCode);

        return Results.NoContent();
    }
}