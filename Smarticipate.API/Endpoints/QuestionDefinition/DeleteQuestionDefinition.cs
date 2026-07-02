using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Smarticipate.API.Data.Identity;

namespace Smarticipate.API.Endpoints.QuestionDefinition;

// "Delete" from the toolbox means un-save. Historical activations and responses are preserved.
public class DeleteQuestionDefinition : IEndpoint
{
    public static void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapDelete("api/question-definitions/{id}", Handle)
            .RequireAuthorization()
            .WithTags("QuestionDefinitions")
            .WithName("Delete Question Definition")
            .Produces(StatusCodes.Status204NoContent)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status404NotFound);
    }

    private static async Task<IResult> Handle(
        int id,
        ClaimsPrincipal user,
        [FromServices] UserDbContext db)
    {
        var userId = user.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(userId)) return Results.Unauthorized();

        var definition = await db.QuestionDefinitions.FirstOrDefaultAsync(d => d.Id == id);
        if (definition is null || definition.OwnerUserId != userId)
            return Results.NotFound();

        definition.IsSaved = false;
        definition.Name = null;
        await db.SaveChangesAsync();
        return Results.NoContent();
    }
}
