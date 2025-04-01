using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Smarticipate.API.Data.Identity;

namespace Smarticipate.API.Endpoints.Session;

public class GetActiveSession : IEndpoint
{
    public static void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapGet("api/sessions/active/{userId}", Handler)
            .WithTags("Sessions")
            .WithName("Get Active Session")
            .WithOpenApi()
            .Produces<ActiveSessionResponse>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status404NotFound);
    }

    public record ActiveSessionResponse(string SessionCode);

    private static async Task<IResult> Handler(
        string userId, 
        [FromServices] UserDbContext db)
    {
        var session = await db.Sessions
            .Where(s => s.UserId == userId && s.EndTime == null)
            .OrderByDescending(s => s.StartTime)
            .FirstOrDefaultAsync();

        if (session is null)
        {
            return Results.NotFound();
        }

        return Results.Ok(new ActiveSessionResponse(session.SessionCode));
    }
}