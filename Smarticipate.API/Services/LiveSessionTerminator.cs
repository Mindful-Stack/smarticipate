using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Smarticipate.API.Data.Identity;
using Smarticipate.API.Hubs;

namespace Smarticipate.API.Services;

// Releases the live state of a session that has ended (or just been auto-closed in
// the DB): writes a final snapshot, clears the in-memory feedback, dismisses any open
// questions so they don't carry into a later run, and tells connected clients it ended.
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

        // Questions belong to a single run — close them out when the session ends.
        var session = await db.Sessions
            .Include(s => s.StudentQuestions)
            .OrderByDescending(s => s.StartTime)
            .FirstOrDefaultAsync(s => s.SessionCode == sessionCode);
        if (session is not null)
        {
            var open = session.StudentQuestions.Where(q => q.DismissedAt == null).ToList();
            if (open.Count > 0)
            {
                foreach (var q in open)
                    q.DismissedAt = DateTime.Now;
                await db.SaveChangesAsync();
            }
        }

        await hub.Clients.Group(sessionCode).SendAsync("SessionEnded");
    }
}
