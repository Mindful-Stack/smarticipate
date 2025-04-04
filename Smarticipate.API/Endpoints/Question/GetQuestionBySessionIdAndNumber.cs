using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Smarticipate.API.Data.Identity;
using Smarticipate.Core;

namespace Smarticipate.API.Endpoints.Question;

public class GetQuestionBySessionIdAndNumber : IEndpoint
{
    public static void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapGet("api/questions/{sessionId}/{questionNumber}", Handler)
            .WithTags("Questions")
            .WithName("Get Question by Session ID and Question Number")
            .WithOpenApi()
            .Produces<QuestionResponse>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status404NotFound);
    }

    private record QuestionResponse(
        int Id,
        int QuestionNumber,
        DateTime? StartTime,
        DateTime? EndTime,
        int SessionId,
        List<ResponseDto> Responses
    );

    private record ResponseDto(
        int Id,
        ResponseOption SelectedOption,
        DateTime TimeStamp,
        int QuestionId
    );

    private static async Task<IResult> Handler(
        int sessionId,
        int questionNumber,
        [FromServices] UserDbContext db
    )
    {
        var question = await db.Questions
            .Include(q => q.Responses)
            .Where(q => q.SessionId == sessionId && q.QuestionNumber == questionNumber)
            .FirstOrDefaultAsync();

        if (question is null)
        {
            return Results.NotFound();
        }

        var response = new QuestionResponse(
            question.Id,
            question.QuestionNumber,
            question.StartTime,
            question.EndTime,
            question.SessionId,
            question.Responses
                .Select(r => new ResponseDto(
                    r.Id,
                    (ResponseOption)r.SelectedOption,
                    r.TimeStamp,
                    r.QuestionId
                )).ToList()
        );

        return Results.Ok(response);
    }
}