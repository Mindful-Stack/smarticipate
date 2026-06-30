using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Smarticipate.API.Data.Identity;
using Smarticipate.Core;

namespace Smarticipate.API.Endpoints.Session;

public class GetActiveSession : IEndpoint
{
    public static void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapGet("api/sessions/active/{userId}", Handle)
            .RequireAuthorization()
            .WithTags("Sessions")
            .WithName("Get Active Session")
            .Produces<ActiveSessionResponse>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden)
            .Produces(StatusCodes.Status404NotFound);
    }

    public record ActiveSessionResponse(
        int Id,
        string SessionCode,
        string? Name,
        DateTime? StartTime,
        string UserId,
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
        DateTime TimeStamp
    );

    private static async Task<IResult> Handle(
        string userId,
        ClaimsPrincipal user,
        [FromServices] UserDbContext db)
    {
        var callerId = user.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(callerId)) return Results.Unauthorized();
        if (callerId != userId) return Results.Forbid();

        var session = await db.Sessions
            .Include(s => s.Questions)
            .ThenInclude(q => q.Responses)
            .Where(s => s.UserId == userId && s.EndTime == null)
            .OrderByDescending(s => s.StartTime)
            .FirstOrDefaultAsync();

        if (session is null)
        {
            return Results.NotFound();
        }
        
        var response = new ActiveSessionResponse(
            session.Id,
            session.SessionCode,
            session.Name,
            session.StartTime,
            session.UserId,
            true,
            session.Questions.Select(q => new QuestionDto(
                q.Id, 
                q.QuestionNumber, 
                q.StartTime,
                q.EndTime,
                q.SessionId,
                q.Responses.Select(r => new ResponseDto(
                    r.Id,
                    (ResponseOption)r.SelectedOption,
                    r.TimeStamp
                )).ToList()
            )).ToList()
        );

        return Results.Ok(response);
    }
}