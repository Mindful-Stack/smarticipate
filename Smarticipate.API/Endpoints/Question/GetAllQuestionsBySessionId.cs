using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Smarticipate.API.Data.Identity;

namespace Smarticipate.API.Endpoints.Question;

public class GetAllQuestionsBySessionId : IEndpoint
{
    public static void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapGet("api/questions/{sessionId}", Handler)
            .WithTags("Questions")
            .WithName("Get Questions by Session")
            .WithOpenApi()
            .Produces<List<QuestionResponse>>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status404NotFound);
    }

    public record QuestionResponse(
        int Id,
        int QuestionNumber,
        DateTime? TimeStamp,
        int SessionId,
        List<ResponseDto> Responses
    );

    public record ResponseDto(
        int Id,
        int SelectedOption,
        DateTime TimeStamp
    );

    private static async Task<IResult> Handler(
        int sessionId,
        [FromServices] UserDbContext db
    )
    {
        var questions = await db.Questions
            .Include(q => q.Responses)
            .Where(q => q.SessionId == sessionId)
            .OrderByDescending(q => q.TimeStamp)
            .ToListAsync();

        if (!questions.Any())
        {
            return Results.NotFound();
        }

        var response = questions
            .Select(q => new QuestionResponse(
                q.Id,
                q.QuestionNumber,
                q.TimeStamp,
                q.SessionId,
                q.Responses
                    .Select(r => new ResponseDto(
                        r.Id,
                        r.SelectedOption,
                        r.TimeStamp)
                    ).ToList()
            ));

        return Results.Ok(response);
    }
}