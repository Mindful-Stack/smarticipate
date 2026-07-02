using System.Security.Claims;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Smarticipate.API.Data.Identity;
using Smarticipate.Core.Entities;

namespace Smarticipate.API.Endpoints.QuestionActivation;

public class FireQuestionActivation : IEndpoint
{
    public static void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPost("api/question-activations", Handle)
            .RequireAuthorization()
            .WithTags("QuestionActivations")
            .WithName("Fire Question Activation")
            .Accepts<Request>("application/json")
            .Produces<Response>(StatusCodes.Status201Created)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status404NotFound);
    }

    public record Request(int DefinitionId, int SessionId, int? DurationSeconds);

    // DurationSeconds is always resolved to a positive value, so the client can drive the
    // int-based SignalR StartQuestion contract directly.
    public record Response(int Id, int DurationSeconds);

    private static async Task<IResult> Handle(
        Request request,
        ClaimsPrincipal user,
        [FromServices] UserDbContext db)
    {
        var userId = user.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(userId)) return Results.Unauthorized();

        var session = await db.Sessions.FirstOrDefaultAsync(s => s.Id == request.SessionId);
        if (session is null || session.UserId != userId)
            return Results.NotFound();

        // Cannot fire into a session that has ended.
        if (session.EndTime is not null)
            return Results.BadRequest(new { error = "This session has ended." });

        var definition = await db.QuestionDefinitions.FirstOrDefaultAsync(d => d.Id == request.DefinitionId);
        // Fire only your own or a system definition.
        if (definition is null || (definition.OwnerUserId != null && definition.OwnerUserId != userId))
            return Results.NotFound();

        // Resolve the timer: explicit override, else the definition's configured default. A fired
        // question is always timed (design section 4), so an unresolved duration is rejected; this also
        // keeps the value compatible with the int-based hub StartQuestion contract.
        var duration = request.DurationSeconds ?? definition.Config.DefaultDurationSeconds;
        if (duration is null or <= 0)
            return Results.BadRequest(new { error = "A positive duration is required (none supplied and the definition has no default)." });

        var activation = new Core.Entities.QuestionActivation
        {
            DefinitionId = definition.Id,
            SessionId = session.Id,
            StartTime = DateTime.UtcNow,
            DurationSeconds = duration.Value
        };

        db.QuestionActivations.Add(activation);
        await db.SaveChangesAsync();

        return TypedResults.Created($"api/question-activations/{activation.Id}",
            new Response(activation.Id, duration.Value));
    }
}
