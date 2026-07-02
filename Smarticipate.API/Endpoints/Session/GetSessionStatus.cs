using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Smarticipate.API.Data.Identity;

namespace Smarticipate.API.Endpoints.Session;

// Anonymous lookup students use to validate a code before joining. Minimal: whether the
// session is live and the activations fired so far (positional, ordered by Id).
public class GetSessionStatus : IEndpoint
{
    public static void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapGet("api/sessions/code/{sessionCode}/status", Handle)
            .WithTags("Sessions")
            .WithName("Get Session Status")
            .Produces<StatusResponse>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status404NotFound);
    }

    public record StatusResponse(int Id, string SessionCode, string? Name, bool IsActive, List<ActivationDto> Activations);
    public record ActivationDto(int Id, int Position);

    private static async Task<IResult> Handle(
        string sessionCode,
        [FromServices] UserDbContext db)
    {
        var session = await db.Sessions
            .Include(s => s.Activations)
            .FirstOrDefaultAsync(s => s.SessionCode == sessionCode);

        if (session is null) return Results.NotFound();

        var activations = session.Activations
            .OrderBy(a => a.Id)
            .Select((a, i) => new ActivationDto(a.Id, i + 1))
            .ToList();

        return Results.Ok(new StatusResponse(
            session.Id, session.SessionCode, session.Name, session.EndTime == null, activations));
    }
}
