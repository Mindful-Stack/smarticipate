using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Smarticipate.API.Data.Identity;

namespace Smarticipate.API.Endpoints.Session;

// Anonymous lookup students use to validate a code before joining. Deliberately minimal: no owner id, no response data, just whether the session is live and the question numbers.
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

    public record StatusResponse(int Id, string SessionCode, string? Name, bool IsActive, List<QuestionDto> Questions);
    public record QuestionDto(int Id, int QuestionNumber);

    private static async Task<IResult> Handle(
        string sessionCode,
        [FromServices] UserDbContext db)
    {
        var session = await db.Sessions
            .Include(s => s.Questions)
            .FirstOrDefaultAsync(s => s.SessionCode == sessionCode);

        if (session is null) return Results.NotFound();

        return Results.Ok(new StatusResponse(
            session.Id,
            session.SessionCode,
            session.Name,
            session.EndTime == null,
            session.Questions
                .OrderBy(q => q.QuestionNumber)
                .Select(q => new QuestionDto(q.Id, q.QuestionNumber))
                .ToList()));
    }
}
