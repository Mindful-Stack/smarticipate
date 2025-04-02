using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Smarticipate.API.Data.Identity;

namespace Smarticipate.API.Endpoints.Question;

public class CreateQuestion : IEndpoint
{
    public static void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPost("api/questions", Handler)
            .WithTags("Questions")
            .WithName("Create Question")
            .Accepts<Request>("application/json")
            .Produces<Response>(StatusCodes.Status201Created);
    }

    private record Request(
        [FromBody] int QuestionNumber,
        [FromBody] DateTime? TimeStamp,
        [FromBody] int SessionId
    );

    private record Response(
        int Id
    );

    private static async Task<Created<Response>> Handler(
        Request request,
        [FromServices] UserDbContext db
        )
    {
        var session = await db.Sessions.FindAsync(request.SessionId);
        
        if (session is null)
        {
            throw new InvalidOperationException($"Session with ID {request.SessionId} not found");
        }

        var newQuestion = new Core.Entities.Question
        {
            QuestionNumber = request.QuestionNumber,
            TimeStamp = request.TimeStamp ?? DateTime.Now,
            SessionId = request.SessionId
        };

        db.Questions.Add(newQuestion);
        await db.SaveChangesAsync();

        var response = new Response(newQuestion.Id);
        return TypedResults.Created($"api/questions/{response.Id}", response);
    }
}