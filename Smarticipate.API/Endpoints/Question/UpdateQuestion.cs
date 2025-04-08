using Microsoft.AspNetCore.Mvc;
using Smarticipate.API.Data.Identity;
using Smarticipate.API.Endpoints.Session;

namespace Smarticipate.API.Endpoints.Question;

public class UpdateQuestion : IEndpoint
{
    public static void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPut("api/questions/{questionId}", Handler)
            .WithTags("Questions")
            .WithName("Update Question")
            .Accepts<Request>("application/json")
            .Produces<Response>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status404NotFound);
    }

    private record Request(
        [FromBody] DateTime? EndTime
    );
    
    private record Response(
        int Id,
        bool Success
    );

    private static async Task<IResult> Handler(
        int questionId,
        Request request,
        [FromServices] UserDbContext db
    )
    {
        var question = await db.Questions.FindAsync(questionId);

        if (question is null)
        {
            return Results.NotFound();
        }

        if (request.EndTime.HasValue)
        {
            question.EndTime = request.EndTime.Value;
        }

        await db.SaveChangesAsync();
        var response = new Response(question.Id, true);
        return Results.Ok(response);
    }
}