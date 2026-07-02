using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Smarticipate.API.Data.Identity;

namespace Smarticipate.API.Endpoints.QuestionActivation;

public class CloseQuestionActivation : IEndpoint
{
    public static void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPost("api/question-activations/{id}/close", Handle)
            .RequireAuthorization()
            .WithTags("QuestionActivations")
            .WithName("Close Question Activation")
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

        var activation = await db.QuestionActivations
            .Include(a => a.Session)
            .FirstOrDefaultAsync(a => a.Id == id);

        if (activation is null || activation.Session.UserId != userId)
            return Results.NotFound();

        activation.EndTime ??= DateTime.UtcNow; // idempotent close
        await db.SaveChangesAsync();
        return Results.NoContent();
    }
}
