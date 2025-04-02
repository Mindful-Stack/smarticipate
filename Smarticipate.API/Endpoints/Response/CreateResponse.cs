using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Smarticipate.API.Data.Identity;

namespace Smarticipate.API.Endpoints.Response;

public class CreateResponse : IEndpoint
{
    public static void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPost("api/responses", Handler)
            .WithTags("Responses")
            .WithName("Create Response")
            .Accepts<Request>("application/json")
            .Produces<Response>(StatusCodes.Status201Created);
    }

    private record Request(
        [FromBody] int SelectedOption,
        [FromBody] DateTime? TimeStamp,
        [FromBody] int QuestionId
    );

    private record Response(
        int Id
    );

    private static async Task<Created<Response>> Handler(
        Request request,
        [FromServices] UserDbContext db)
    {
        var question = await db.Questions.FindAsync(request.QuestionId);

        if (question is null)
        {
            throw new InvalidOperationException($"Question with ID {request.QuestionId} not found");
        }

        var newResponse = new Core.Entities.Response
        {
            SelectedOption = request.SelectedOption,
            TimeStamp = request.TimeStamp ?? DateTime.Now,
            QuestionId = request.QuestionId
        };

        db.Responses.Add(newResponse);
        await db.SaveChangesAsync();

        var response = new Response(newResponse.Id);
        return TypedResults.Created($"api/questions/{response.Id}", response);
    }
}