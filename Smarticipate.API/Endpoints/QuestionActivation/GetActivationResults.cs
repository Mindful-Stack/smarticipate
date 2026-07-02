using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Smarticipate.API.Data.Identity;
using Smarticipate.API.QuestionTypes;

namespace Smarticipate.API.Endpoints.QuestionActivation;

public class GetActivationResults : IEndpoint
{
    public static void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapGet("api/question-activations/{id}/results", Handle)
            .RequireAuthorization()
            .WithTags("QuestionActivations")
            .WithName("Get Activation Results")
            .Produces<QuestionResult>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status404NotFound);
    }

    private static async Task<IResult> Handle(
        int id,
        ClaimsPrincipal user,
        [FromServices] UserDbContext db,
        [FromServices] QuestionTypeRegistry registry)
    {
        var userId = user.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(userId)) return Results.Unauthorized();

        var activation = await db.QuestionActivations
            .Include(a => a.Session)
            .Include(a => a.Definition).ThenInclude(d => d.Options)
            .Include(a => a.Responses).ThenInclude(r => r.Selections)
            .AsSplitQuery() // two sibling collections (Options and Responses) would otherwise multiply rows
            .FirstOrDefaultAsync(a => a.Id == id);

        if (activation is null || activation.Session.UserId != userId)
            return Results.NotFound();

        var result = registry.For(activation.Definition.Type).Aggregate(activation);
        return Results.Ok(result);
    }
}
