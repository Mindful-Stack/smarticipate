using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Smarticipate.API.Data.Identity;
using Smarticipate.Core;

namespace Smarticipate.API.Endpoints.Response;

public class GetAllResponsesByQuestionId : IEndpoint
{
    public static void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapGet("api/responses/{questionId}", Handler)
            .WithTags("Responses")
            .WithName("Get Responses by Question Id")
            .WithOpenApi()
            .Produces<List<Response>>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status404NotFound);
    }

    private record Response(
        int Id,
        ResponseOption SelectedOption,
        DateTime TimeStamp,
        int QuestionId
    );

    private static async Task<IResult> Handler(
        int questionId, 
        [FromServices] UserDbContext db)
    {
        var responses = await db.Responses
            .Where(r => r.QuestionId == questionId)
            .OrderByDescending(q => q.TimeStamp)
            .ToListAsync();

        if (!responses.Any())
        {
            return Results.NotFound();
        }

        var response = responses
            .Select(r => new Response(
                r.Id,
                (ResponseOption)r.SelectedOption,
                r.TimeStamp,
                r.QuestionId
            )).ToList();
        return Results.Ok(response);
    }
}