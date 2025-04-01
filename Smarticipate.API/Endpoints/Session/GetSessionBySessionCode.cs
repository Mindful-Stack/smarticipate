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
        string sessionCode,
        [FromServices] UserDbContext db
    )
    {
        var session = await db.Sessions
            .Include(s => s.Questions)
            .ThenInclude(q => q.Responses)
            .FirstOrDefaultAsync(s => s.SessionCode == sessionCode);

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
            session.EndTime == null,
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