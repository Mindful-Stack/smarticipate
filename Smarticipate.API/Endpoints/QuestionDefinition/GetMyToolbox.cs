using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Smarticipate.API.Data.Identity;
using Smarticipate.Core;

namespace Smarticipate.API.Endpoints.QuestionDefinition;

public class GetMyToolbox : IEndpoint
{
    public static void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapGet("api/question-definitions", Handle)
            .RequireAuthorization()
            .WithTags("QuestionDefinitions")
            .WithName("Get My Toolbox")
            .Produces<List<DefinitionResponse>>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status401Unauthorized);
    }

    public record DefinitionResponse(
        int Id,
        QuestionType Type,
        string Prompt,
        string? Name,
        bool IsSystem,
        string ConfigJson,
        List<OptionDto> Options
    );

    public record OptionDto(int Id, string Text, int Ordinal);

    private static async Task<IResult> Handle(
        ClaimsPrincipal user,
        [FromServices] UserDbContext db)
    {
        var userId = user.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(userId)) return Results.Unauthorized();

        var definitions = await db.QuestionDefinitions
            .Include(d => d.Options)
            .Where(d => d.OwnerUserId == null || (d.OwnerUserId == userId && d.IsSaved))
            .OrderBy(d => d.OwnerUserId == null ? 0 : 1) // system first
            .ThenBy(d => d.Name)
            .ToListAsync();

        var response = definitions.Select(d => new DefinitionResponse(
            d.Id,
            d.Type,
            d.Prompt,
            d.Name,
            d.OwnerUserId == null,
            d.ConfigJson,
            d.Options.OrderBy(o => o.Ordinal).Select(o => new OptionDto(o.Id, o.Text, o.Ordinal)).ToList()
        )).ToList();

        return Results.Ok(response);
    }
}
