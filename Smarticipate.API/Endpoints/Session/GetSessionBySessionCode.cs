using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Smarticipate.API.Data.Identity;

namespace Smarticipate.API.Endpoints.Session;

public class GetSessionBySessionCode : IEndpoint
{
    public static void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapGet("api/sessions/code/{sessionCode}", Handler)
            .WithTags("Sessions")
            .WithName("Get Session by Session Code")
            .WithOpenApi()
            .Produces<SessionResponse>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status404NotFound);
    }

    public record SessionResponse(
        int Id,
        string SessionCode,
        DateTime? StartTime,
        DateTime? EndTime,
        string UserId,
        bool IsActive
    );

    private static async Task<IResult> Handler(
        string sessionCode,
        [FromServices] UserDbContext db
    )
    {
        var session = await db.Sessions.FirstOrDefaultAsync(s => s.SessionCode == sessionCode);

        if (session is null)
        {
            return Results.NotFound();
        }

        var response = new SessionResponse(
            session.Id,
            session.SessionCode,
            session.StartTime,
            session.EndTime,
            session.UserId,
            session.EndTime == null
        );

        return Results.Ok(response);
    }
}