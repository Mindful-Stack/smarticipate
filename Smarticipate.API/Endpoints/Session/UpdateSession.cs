using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Smarticipate.API.Data.Identity;

namespace Smarticipate.API.Endpoints.Session;

public class UpdateSession : IEndpoint
{
    public static void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPut("api/sessions/{sessionCode}", Handler)
            .WithTags("Sessions")
            .WithName("Update Session")
            .WithOpenApi()
            .Accepts<Request>("application/json")
            .Produces(StatusCodes.Status204NoContent)
            .Produces(StatusCodes.Status404NotFound);
    }

    public record Request(
        [FromBody] DateTime? EndTime
    );

    private static async Task<IResult> Handler(
        string sessionCode,
        Request request,
        [FromServices] UserDbContext db)
    {
        var session = await db.Sessions.FirstOrDefaultAsync(s => s.SessionCode == sessionCode);

        if (session is null)
        {
            return Results.NotFound();
        }

        session.EndTime = request.EndTime;
        await db.SaveChangesAsync();
        return Results.NoContent();
    }
}