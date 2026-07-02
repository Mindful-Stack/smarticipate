using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Smarticipate.API.Data.Identity;
using Smarticipate.Core;

namespace Smarticipate.API.Endpoints.Session;

public class GetSessionBySessionCode : IEndpoint
{
    public static void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapGet("api/sessions/code/{sessionCode}", Handle)
            .RequireAuthorization()
            .WithTags("Sessions")
            .WithName("Get Session by Session Code")
            .Produces<SessionResponse>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status404NotFound);
    }

    public record SessionResponse(
        int Id,
        string SessionCode,
        DateTime? StartTime,
        DateTime? EndTime,
        string UserId,
        bool IsActive,
        List<ActivationDto> Activations
    );

    public record ActivationDto(
        int Id,
        int Position,
        Smarticipate.Core.QuestionType Type,
        string Prompt,
        DateTime StartTime,
        DateTime? EndTime
    );

    private static async Task<IResult> Handle(
        string sessionCode,
        ClaimsPrincipal user,
        [FromServices] UserDbContext db
    )
    {
        var userId = user.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(userId)) return Results.Unauthorized();

        var session = await db.Sessions
            .Include(s => s.Activations)
            .ThenInclude(a => a.Definition)
            .FirstOrDefaultAsync(s => s.SessionCode == sessionCode);

        if (session is null || session.UserId != userId)
        {
            return Results.NotFound();
        }

        var ordered = session.Activations.OrderBy(a => a.Id).ToList();
        var activations = ordered
            .Select((a, i) => new ActivationDto(a.Id, i + 1, a.Definition.Type, a.Definition.Prompt, a.StartTime, a.EndTime))
            .ToList();

        var response = new SessionResponse(
            session.Id,
            session.SessionCode,
            session.StartTime,
            session.EndTime,
            session.UserId,
            session.EndTime == null,
            activations
        );

        return Results.Ok(response);
    }
}
