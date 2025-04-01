using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Smarticipate.API.Data.Identity;

namespace Smarticipate.API.Endpoints.Session;

public class CreateSession : IEndpoint
{
    public static void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPost("api/sessions", Handler)
            .WithTags("Sessions")
            .WithName("Create Session")
            .WithOpenApi()
            .Accepts<Request>("application/json")
            .Produces<Response>(StatusCodes.Status201Created);
    }

    public record Request(
        [FromBody] string SessionCode,
        [FromBody] DateTime? StartTime,
        [FromBody] DateTime? EndTime,
        [FromBody] string UserId
    );

    public record Response(
        int Id
    );

    private static async Task<Created<Response>> Handler(
        Request request,
        [FromServices] UserDbContext db)
    {
        var newSession = new Core.Entities.Session
        {
            SessionCode = request.SessionCode,
            StartTime = request.StartTime,
            EndTime = request.EndTime,
            UserId = request.UserId
        };

        db.Sessions.Add(newSession);
        await db.SaveChangesAsync();

        var response = new Response(newSession.Id);
        return TypedResults.Created($"api/sessions/{response.Id}", response);
    }
}