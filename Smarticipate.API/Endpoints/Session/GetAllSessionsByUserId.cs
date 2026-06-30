using System.Security.Claims;
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
        app.MapGet("api/sessions/{userId}", Handle)
            .RequireAuthorization()
            .WithTags("Sessions")
            .WithName("Get Sessions by User")
            .Produces<List<SessionResponse>>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden)
            .Produces(StatusCodes.Status404NotFound);
    }

    public record SessionResponse(
        int Id,
        string SessionCode,
        string? Name,
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

    private static async Task<IResult> Handle(
        string userId,
        ClaimsPrincipal user,
        [FromServices] UserDbContext db)
    {
        var callerId = user.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(callerId)) return Results.Unauthorized();
        if (callerId != userId) return Results.Forbid();

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
                s.Name,
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