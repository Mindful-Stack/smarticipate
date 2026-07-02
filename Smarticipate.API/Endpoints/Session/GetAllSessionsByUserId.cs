using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Smarticipate.API.Data.Identity;
using Smarticipate.Core;

namespace Smarticipate.API.Endpoints.Session;

public class GetAllSessionsByUserId : IEndpoint
{
    public static void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapGet("api/sessions/{userId}", Handle)
            .RequireAuthorization()
            .WithTags("Sessions")
            .WithName("Get Sessions by User")
            .Produces<List<SessionResponse>>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden)
            .Produces(StatusCodes.Status404NotFound);
    }

    public record SessionResponse(
        int Id,
        string SessionCode,
        string? Name,
        DateTime? StartTime,
        DateTime? EndTime,
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
        string userId,
        ClaimsPrincipal user,
        [FromServices] UserDbContext db)
    {
        var callerId = user.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(callerId)) return Results.Unauthorized();
        if (callerId != userId) return Results.Forbid();

        var sessions = await db.Sessions
            .Include(s => s.Activations)
            .ThenInclude(a => a.Definition)
            .Where(s => s.UserId == userId)
            .OrderByDescending(s => s.StartTime)
            .ToListAsync();

        if (!sessions.Any())
        {
            return Results.NotFound();
        }

        var response = sessions
            .Select(s =>
            {
                var ordered = s.Activations.OrderBy(a => a.Id).ToList();
                var activations = ordered
                    .Select((a, i) => new ActivationDto(a.Id, i + 1, a.Definition.Type, a.Definition.Prompt, a.StartTime, a.EndTime))
                    .ToList();

                return new SessionResponse(
                    s.Id,
                    s.SessionCode,
                    s.Name,
                    s.StartTime,
                    s.EndTime,
                    s.EndTime == null,
                    activations
                );
            }).ToList();

        return Results.Ok(response);
    }
}