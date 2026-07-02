using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Smarticipate.API.Data.Identity;
using Smarticipate.API.QuestionTypes;
using Smarticipate.Core.Entities;

namespace Smarticipate.API.Endpoints.Response;

// Students have no accounts, so this stays anonymous. One response per participant per
// activation; submitting again revises the existing answer.
public class SubmitResponse : IEndpoint
{
    public static void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPost("api/responses", Handle)
            .WithTags("Responses")
            .WithName("Submit Response")
            .Accepts<Request>("application/json")
            .Produces<Response>(StatusCodes.Status200OK)
            .Produces<Response>(StatusCodes.Status201Created)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status404NotFound)
            .Produces(StatusCodes.Status409Conflict);
    }

    public record Request(
        int ActivationId,
        string ParticipantKey,
        decimal? Numeric,
        string? Text,
        string[]? Words,
        List<int>? OptionIds
    );

    public record Response(int Id);

    private static async Task<IResult> Handle(
        Request request,
        [FromServices] UserDbContext db,
        [FromServices] QuestionTypeRegistry registry)
    {
        if (string.IsNullOrWhiteSpace(request.ParticipantKey))
            return Results.BadRequest(new { error = "A participant key is required." });

        var activation = await db.QuestionActivations
            .Include(a => a.Session)
            .Include(a => a.Definition).ThenInclude(d => d.Options)
            .FirstOrDefaultAsync(a => a.Id == request.ActivationId);

        if (activation is null)
            return Results.NotFound();

        // Reject once the question is closed or its session has ended. Ending a session closes its
        // open activations (LiveSessionTerminator), but check the session too as defence in depth.
        if (activation.EndTime is not null || activation.Session.EndTime is not null)
            return Results.BadRequest(new { error = "This question is closed." });

        // Find existing response for this participant (revise) or create a new one.
        var response = await db.Responses
            .Include(r => r.Selections)
            .FirstOrDefaultAsync(r => r.ActivationId == activation.Id && r.ParticipantKey == request.ParticipantKey);

        var isNew = response is null;
        if (response is null)
        {
            response = new Core.Entities.Response
            {
                ActivationId = activation.Id,
                ParticipantKey = request.ParticipantKey
            };
            db.Responses.Add(response);
        }
        else
        {
            // Revise: clear every channel so the handler writes a clean single channel.
            db.ResponseSelections.RemoveRange(response.Selections);
            response.Selections.Clear();
            response.NumericValue = null;
            response.TextValue = null;
        }

        response.SubmittedAt = DateTime.UtcNow;

        var input = new ResponseInput(request.Numeric, request.Text, request.Words, request.OptionIds);

        try
        {
            registry.For(activation.Definition.Type).PopulateResponse(activation, response, input);
        }
        catch (QuestionValidationException ex)
        {
            return Results.BadRequest(new { error = ex.Message });
        }

        try
        {
            await db.SaveChangesAsync();
        }
        catch (DbUpdateException ex) when (IsParticipantKeyConflict(ex))
        {
            // Two first-submits from the same participant raced on the unique (ActivationId, ParticipantKey)
            // index. Signal a retry; the retry takes the revise path. Any other DB error is NOT swallowed.
            return Results.Conflict(new { error = "A response for this participant is being recorded; retry." });
        }

        return isNew
            ? TypedResults.Created($"api/responses/{response.Id}", new Response(response.Id))
            : Results.Ok(new Response(response.Id));
    }

    // True only for a unique violation on the participant-key index, so unrelated DB failures surface.
    private static bool IsParticipantKeyConflict(DbUpdateException ex) =>
        ex.InnerException is PostgresException { SqlState: "23505" } pg
        && pg.ConstraintName?.Contains("ActivationId_ParticipantKey", StringComparison.Ordinal) == true;
}
