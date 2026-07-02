using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Smarticipate.API.Data.Identity;

namespace Smarticipate.API.Endpoints.QuestionDefinition;

public class SaveQuestionDefinition : IEndpoint
{
    public static void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPost("api/question-definitions/{id}/save", Handle)
            .RequireAuthorization()
            .WithTags("QuestionDefinitions")
            .WithName("Save Question Definition")
            .Accepts<Request>("application/json")
            .Produces(StatusCodes.Status204NoContent)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status404NotFound);
    }

    public record Request(string Name);

    private static async Task<IResult> Handle(
        int id,
        Request request,
        ClaimsPrincipal user,
        [FromServices] UserDbContext db)
    {
        var userId = user.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(userId)) return Results.Unauthorized();

        var name = request.Name?.Trim();
        if (string.IsNullOrWhiteSpace(name))
            return Results.BadRequest(new { error = "A name is required to save to the toolbox." });

        var definition = await db.QuestionDefinitions.FirstOrDefaultAsync(d => d.Id == id);

        // "Not yours" == "not found". System definitions (null owner) cannot be promoted.
        if (definition is null || definition.OwnerUserId != userId)
            return Results.NotFound();

        definition.Name = name;
        definition.IsSaved = true;
        await db.SaveChangesAsync();
        return Results.NoContent();
    }
}
