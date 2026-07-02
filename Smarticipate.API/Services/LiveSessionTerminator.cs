using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Smarticipate.API.Data.Identity;
using Smarticipate.API.Hubs;

namespace Smarticipate.API.Services;

// Releases the live state of a session that has ended (or just been auto-closed in the DB):
// writes a final snapshot, clears in-memory feedback, dismisses open questions, closes open
// activations so late answers cannot land, and tells connected clients it ended.
public static class LiveSessionTerminator
{
    public static async Task EndAsync(
        UserDbContext db,
        LiveFeedbackStore store,
        IHubContext<SessionHub> hub,
        string sessionCode)
    {
        await FeedbackSnapshotWriter.WriteAsync(db, store, sessionCode);
        store.Reset(sessionCode);
        SessionHub.DropActiveQuestion(sessionCode);

        // A run's questions and activations belong to that run; close them out when the session ends.
        var session = await db.Sessions
            .Include(s => s.StudentQuestions)
            .Include(s => s.Activations)
            .OrderByDescending(s => s.StartTime)
            .FirstOrDefaultAsync(s => s.SessionCode == sessionCode);
        if (session is not null)
        {
            var openQuestions = session.StudentQuestions.Where(q => q.DismissedAt == null).ToList();
            foreach (var q in openQuestions)
                q.DismissedAt = DateTime.Now; // unchanged from today

            var openActivations = session.Activations.Where(a => a.EndTime == null).ToList();
            foreach (var a in openActivations)
                a.EndTime = DateTime.UtcNow; // new timestamps are UTC (design decision)

            if (openQuestions.Count > 0 || openActivations.Count > 0)
                await db.SaveChangesAsync();
        }

        await hub.Clients.Group(sessionCode).SendAsync("SessionEnded");
    }
}
