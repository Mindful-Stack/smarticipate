using System.Security.Claims;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Smarticipate.API.Data.Identity;
using Smarticipate.API.QuestionTypes;
using Smarticipate.Core;
using Smarticipate.Core.Entities;

namespace Smarticipate.API.Endpoints.QuestionDefinition;

public class CreateQuestionDefinition : IEndpoint
{
    public static void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPost("api/question-definitions", Handle)
            .RequireAuthorization()
            .WithTags("QuestionDefinitions")
            .WithName("Create Question Definition")
            .Accepts<Request>("application/json")
            .Produces<Response>(StatusCodes.Status201Created)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status401Unauthorized);
    }

    public record OptionInput(string Text, bool IsCorrect = false);

    public record Request(
        QuestionType Type,
        string Prompt,
        List<OptionInput>? Options,
        QuestionConfigView? Config
    );

    public record Response(int Id);

    private static async Task<IResult> Handle(
        Request request,
        ClaimsPrincipal user,
        [FromServices] UserDbContext db,
        [FromServices] QuestionTypeRegistry registry)
    {
        var userId = user.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(userId)) return Results.Unauthorized();

        if (!registry.Supports(request.Type))
            return Results.BadRequest(new { error = $"Question type {request.Type} is not supported yet." });

        if (string.IsNullOrWhiteSpace(request.Prompt))
            return Results.BadRequest(new { error = "A prompt is required." });

        // Build options. YesNo is stored as two auto-created options (design section 2); when the
        // client sends none, materialise Yes/No here so a ready-check flows through the same machinery.
        var optionInputs = request.Options ?? [];
        if (request.Type == QuestionType.YesNo && optionInputs.Count == 0)
            optionInputs = [new OptionInput("Yes"), new OptionInput("No")];

        // Guard null/blank option text before Trim so bad input is a clean 400, not an NRE 500.
        if (optionInputs.Any(o => string.IsNullOrWhiteSpace(o.Text)))
            return Results.BadRequest(new { error = "Every option needs text." });

        var definition = new Core.Entities.QuestionDefinition
        {
            Type = request.Type,
            Prompt = request.Prompt.Trim(),
            OwnerUserId = userId,
            IsSaved = false,
            Config = request.Config ?? new QuestionConfigView(),
            Options = optionInputs
                .Select((o, i) => new QuestionOption { Text = o.Text.Trim(), Ordinal = i, IsCorrect = o.IsCorrect })
                .ToList()
        };

        try
        {
            registry.For(definition.Type).ValidateDefinition(definition);
        }
        catch (QuestionValidationException ex)
        {
            return Results.BadRequest(new { error = ex.Message });
        }

        db.QuestionDefinitions.Add(definition);
        await db.SaveChangesAsync();

        return TypedResults.Created($"api/question-definitions/{definition.Id}", new Response(definition.Id));
    }
}
