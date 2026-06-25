using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Smarticipate.API.Data.Identity;

namespace Smarticipate.API.Endpoints.Session;

public class CreateSession : IEndpoint
{
    public static void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPost("api/sessions", Handle)
            .WithTags("Sessions")
            .WithName("Create Session")
            .Accepts<Request>("application/json")
            .Produces<Response>(StatusCodes.Status201Created);
    }

    public record Request(
        [FromBody] string SessionCode,
        [FromBody] string? Name,
        [FromBody] DateTime? StartTime,
        [FromBody] DateTime? EndTime,
        [FromBody] string UserId
    );

    public record Response(
        int Id
    );

    private static async Task<Created<Response>> Handle(
        Request request,
        [FromServices] UserDbContext db)
    {
        // Enforce one active session per teacher: close any currently-open ones first so GetActive (orders by StartTime desc) is unambiguous
        var openSessions = await db.Sessions
            .Where(s => s.UserId == request.UserId && s.EndTime == null)
            .ToListAsync();
        foreach (var s in openSessions)
        {
            s.EndTime = DateTime.Now;
        }
        
        var newSession = new Core.Entities.Session
        {
            SessionCode = request.SessionCode,
            Name = request.Name,
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