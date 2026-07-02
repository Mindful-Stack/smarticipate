using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Smarticipate.API.Data.Identity;
using Smarticipate.Core;

namespace Smarticipate.API.Endpoints.QuestionDefinition;

public class GetQuestionDefinition : IEndpoint
{
    public static void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapGet("api/question-definitions/{id}", Handle)
            .RequireAuthorization()
            .WithTags("QuestionDefinitions")
            .WithName("Get Question Definition")
            .Produces<DefinitionResponse>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status404NotFound);
    }

    public record DefinitionResponse(
        int Id,
        QuestionType Type,
        string Prompt,
        string? Name,
        bool IsSaved,
        bool IsSystem,
        string ConfigJson,
        List<OptionDto> Options
    );

    public record OptionDto(int Id, string Text, int Ordinal);

    private static async Task<IResult> Handle(
        int id,
        ClaimsPrincipal user,
        [FromServices] UserDbContext db)
    {
        var userId = user.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(userId)) return Results.Unauthorized();

        var d = await db.QuestionDefinitions
            .Include(x => x.Options)
            .FirstOrDefaultAsync(x => x.Id == id);

        if (d is null || (d.OwnerUserId != null && d.OwnerUserId != userId))
            return Results.NotFound();

        return Results.Ok(new DefinitionResponse(
            d.Id, d.Type, d.Prompt, d.Name, d.IsSaved, d.OwnerUserId == null, d.ConfigJson,
            d.Options.OrderBy(o => o.Ordinal).Select(o => new OptionDto(o.Id, o.Text, o.Ordinal)).ToList()));
    }
}
