using Microsoft.EntityFrameworkCore;
using Smarticipate.API.Data.Identity;
using Smarticipate.Core.Entities;

namespace Smarticipate.API.Services;

public static class FeedbackSnapshotWriter
{
    public static async Task WriteAsync(UserDbContext db, LiveFeedbackStore store, string sessionCode)
    {
        var agg = store.GetAggregate(sessionCode);
        if (agg.RespondentCount == 0) return;

        var session = await db.Sessions
            .OrderByDescending(s => s.StartTime)
            .FirstOrDefaultAsync(s => s.SessionCode == sessionCode);
        if (session is null) return;

        db.FeedbackSnapshots.Add(new FeedbackSnapshot
        {
            SessionId = session.Id,
            Timestamp = DateTime.Now,
            RespondentCount = agg.RespondentCount,
            PaceCounts = agg.PaceCounts,
            UnderstandingCounts = agg.UnderstandingCounts
        });
        await db.SaveChangesAsync();
    }
}