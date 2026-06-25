using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Smarticipate.API.Data.Identity;

namespace Smarticipate.API.Endpoints.Session;

public class RestartSession : IEndpoint
{
    public static void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPut("api/sessions/{sessionCode}/restart", Handle)
            .WithTags("Sessions")
            .WithName("Restart Session")
            .Produces(StatusCodes.Status204NoContent)
            .Produces(StatusCodes.Status404NotFound);
    }

    private static async Task<IResult> Handle(
        string sessionCode,
        [FromServices] UserDbContext db)
    {
        var session = await db.Sessions
            .OrderByDescending(s => s.StartTime)
            .FirstOrDefaultAsync(s => s.SessionCode == sessionCode);

        if (session is null) return Results.NotFound();
        
        // Enforce one active session per teacher: close any currently-open ones first. Without this, GetActiveSession (orders by StartTime desc) could load the wrong one.        
        var others = await db.Sessions
            .Where(s => s.UserId == session.UserId && s.EndTime == null && s.Id != session.Id)
            .ToListAsync();
        foreach (var o in others)
            o.EndTime = DateTime.Now;

        session.EndTime = null;             // active again
        session.StartTime = DateTime.Now;   // bump so it sorts to the top in GetActiveSession
        await db.SaveChangesAsync();
        return Results.NoContent();
        
    }
}