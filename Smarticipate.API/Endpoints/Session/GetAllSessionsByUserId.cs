using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Smarticipate.API.Data.Identity;
using Smarticipate.Core;

namespace Smarticipate.API.Endpoints.Session;

public class GetAllSessionsByUserId : IEndpoint
{
    public static void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapGet("api/sessions/{userId}", Handler)
            .WithTags("Sessions")
            .WithName("Get Sessions by User")
            .WithOpenApi()
            .Produces<List<SessionResponse>>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status404NotFound);
    }

    public record SessionResponse(
        int Id,
        string SessionCode,
        DateTime? StartTime,
        DateTime? EndTime,
        bool IsActive,
        List<QuestionDto> Questions
    );

    public record QuestionDto(
        int Id,
        int QuestionNumber,
        DateTime? StartTime,
        DateTime? EndTime,
        int SessionId,
        List<ResponseDto> Responses
    );

    public record ResponseDto(
        int Id,
        ResponseOption SelectedOption,
        DateTime TimeStamp,
        int QuestionId
    );

    private static async Task<IResult> Handler(
        string userId,
        [FromServices] UserDbContext db)
    {
        var sessions = await db.Sessions
            .Include(s => s.Questions)
            .ThenInclude(q => q.Responses)
            .Where(s => s.UserId == userId)
            .OrderByDescending(s => s.StartTime)
            .ToListAsync();

        if (!sessions.Any())
        {
            return Results.NotFound();
        }

        var response = sessions
            .Select(s => new SessionResponse(
                s.Id,
                s.SessionCode,
                s.StartTime,
                s.EndTime,
                s.EndTime == null,
                s.Questions
                    .Select(q => new QuestionDto(
                        q.Id,
                        q.QuestionNumber,
                        q.StartTime,
                        q.EndTime,
                        q.SessionId,
                        q.Responses.Select(r => new ResponseDto(
                            r.Id,
                            (ResponseOption)r.SelectedOption,
                            r.TimeStamp,
                            r.QuestionId
                        )).ToList()
                    )).ToList()
            )).ToList();

        return Results.Ok(response);
    }
}