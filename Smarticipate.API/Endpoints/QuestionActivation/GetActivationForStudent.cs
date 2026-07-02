using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Smarticipate.API.Data.Identity;
using Smarticipate.Core;

namespace Smarticipate.API.Endpoints.QuestionActivation;

// Anonymous: the student client fetches this on the SignalR QuestionStarted event to render
// the per-type UI. No responses, no owner id, no results.
public class GetActivationForStudent : IEndpoint
{
    public static void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapGet("api/question-activations/{id}", Handle)
            .WithTags("QuestionActivations")
            .WithName("Get Activation For Student")
            .Produces<ActivationResponse>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status404NotFound);
    }

    public record ActivationResponse(
        int Id,
        QuestionType Type,
        string Prompt,
        string ConfigJson,
        DateTime StartTime,
        DateTime? EndTime,
        int? DurationSeconds,
        List<OptionDto> Options
    );

    public record OptionDto(int Id, string Text, int Ordinal);

    private static async Task<IResult> Handle(
        int id,
        [FromServices] UserDbContext db)
    {
        var a = await db.QuestionActivations
            .Include(x => x.Definition).ThenInclude(d => d.Options)
            .FirstOrDefaultAsync(x => x.Id == id);

        if (a is null) return Results.NotFound();

        return Results.Ok(new ActivationResponse(
            a.Id,
            a.Definition.Type,
            a.Definition.Prompt,
            a.Definition.ConfigJson,
            a.StartTime,
            a.EndTime,
            a.DurationSeconds,
            a.Definition.Options.OrderBy(o => o.Ordinal)
                .Select(o => new OptionDto(o.Id, o.Text, o.Ordinal)).ToList()));
    }
}
