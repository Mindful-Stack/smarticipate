using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Smarticipate.API.Data.Identity;

namespace Smarticipate.API.Endpoints.Session;

public class GetLatestSnapshot : IEndpoint
{
    public static void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapGet("api/sessions/{sessionCode}/snapshot/latest", Handle)
            .WithTags("Sessions")
            .WithName("Get Latest Feedback Snapshot")
            .Produces<SnapshotResponse>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status404NotFound);
    }

    // Descriptive name (not bare `Response`) to match the other GET endpoints' DTO convention
    // and keep the OpenAPI/Scalar schema id readable.
    public record SnapshotResponse(
        int RespondentCount,
        int[] PaceCounts,
        int[] UnderstandingCounts,
        DateTime Timestamp
    );

    private static async Task<IResult> Handle(
        string sessionCode,
        [FromServices] UserDbContext db)
    {
        var snapshot = await db.FeedbackSnapshots
            .Where(fs => fs.Session.SessionCode == sessionCode)
            .OrderByDescending(fs => fs.Timestamp)
            .FirstOrDefaultAsync();

        if (snapshot is null)
            return Results.NotFound();

        return Results.Ok(new SnapshotResponse(
            snapshot.RespondentCount,
            snapshot.PaceCounts,
            snapshot.UnderstandingCounts,
            snapshot.Timestamp));
    }
}
