using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Smarticipate.API.Data.Identity;
using Smarticipate.Core;

namespace Smarticipate.API.Endpoints.Session;

public class GetActiveSession : IEndpoint
{
    public static void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapGet("api/sessions/active/{userId}", Handle)
            .RequireAuthorization()
            .WithTags("Sessions")
            .WithName("Get Active Session")
            .Produces<ActiveSessionResponse>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden)
            .Produces(StatusCodes.Status404NotFound);
    }

    public record ActiveSessionResponse(
        int Id,
        string SessionCode,
        string? Name,
        DateTime? StartTime,
        string UserId,
        bool IsActive,
        List<ActivationDto> Activations
        );

    public record ActivationDto(
        int Id,
        int Position,
        QuestionType Type,
        string Prompt,
        DateTime StartTime,
        DateTime? EndTime
    );

    private static async Task<IResult> Handle(
        string userId,
        ClaimsPrincipal user,
        [FromServices] UserDbContext db)
    {
        var callerId = user.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(callerId)) return Results.Unauthorized();
        if (callerId != userId) return Results.Forbid();

        var session = await db.Sessions
            .Include(s => s.Activations)
            .ThenInclude(a => a.Definition)
            .Where(s => s.UserId == userId && s.EndTime == null)
            .OrderByDescending(s => s.StartTime)
            .FirstOrDefaultAsync();

        if (session is null)
        {
            return Results.NotFound();
        }

        var ordered = session.Activations.OrderBy(a => a.Id).ToList();
        var activations = ordered
            .Select((a, i) => new ActivationDto(a.Id, i + 1, a.Definition.Type, a.Definition.Prompt, a.StartTime, a.EndTime))
            .ToList();

        var response = new ActiveSessionResponse(
            session.Id,
            session.SessionCode,
            session.Name,
            session.StartTime,
            session.UserId,
            true,
            activations
        );

        return Results.Ok(response);
    }
}