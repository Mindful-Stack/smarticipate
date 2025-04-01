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

    public record ActiveSessionResponse(
        int Id,
        string SessionCode,
        DateTime? StartTime,
        string UserId,
        bool IsActive,
        List<QuestionDto> Questions
        );
    
    public record QuestionDto(
        int Id,
        int QuestionNumber,
        List<ResponseDto> Responses
    );
    
    public record ResponseDto(
        int Id,
        int SelectedOption,
        DateTime TimeStamp
    );

    private static async Task<IResult> Handler(
        string userId, 
        [FromServices] UserDbContext db)
    {
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
            session.StartTime,
            session.UserId,
            true,
            session.Questions.Select(q => new QuestionDto(
                q.Id,
                q.QuestionNumber,
                q.Responses.Select(r => new ResponseDto(
                    r.Id,
                    r.SelectedOption,
                    r.TimeStamp
                )).ToList()
            )).ToList()
        );

        return Results.Ok(response);
    }
}