using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Smarticipate.API.Data.Identity;
using Smarticipate.Core;

namespace Smarticipate.API.Endpoints.QuestionActivation;

public class GetActivationsBySession : IEndpoint
{
    public static void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapGet("api/question-activations/session/{sessionId}", Handle)
            .RequireAuthorization()
            .WithTags("QuestionActivations")
            .WithName("Get Activations By Session")
            .Produces<List<ActivationSummary>>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status404NotFound);
    }

    // Position is 1-based firing order derived from Id ordering, not a stored field.
    public record ActivationSummary(
        int Id,
        int Position,
        QuestionType Type,
        string Prompt,
        DateTime StartTime,
        DateTime? EndTime,
        int ResponseCount
    );

    private static async Task<IResult> Handle(
        int sessionId,
        ClaimsPrincipal user,
        [FromServices] UserDbContext db)
    {
        var userId = user.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(userId)) return Results.Unauthorized();

        var session = await db.Sessions.FirstOrDefaultAsync(s => s.Id == sessionId);
        if (session is null || session.UserId != userId)
            return Results.NotFound();

        var activations = await db.QuestionActivations
            .Where(a => a.SessionId == sessionId)
            .Include(a => a.Definition)
            .OrderBy(a => a.Id)
            .Select(a => new
            {
                a.Id,
                a.Definition.Type,
                a.Definition.Prompt,
                a.StartTime,
                a.EndTime,
                ResponseCount = a.Responses.Count
            })
            .ToListAsync();

        var response = activations
            .Select((a, i) => new ActivationSummary(a.Id, i + 1, a.Type, a.Prompt, a.StartTime, a.EndTime, a.ResponseCount))
            .ToList();

        return Results.Ok(response);
    }
}
