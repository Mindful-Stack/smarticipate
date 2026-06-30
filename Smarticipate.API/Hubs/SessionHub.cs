using System.Security.Claims;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Smarticipate.API.Data.Identity;
using Smarticipate.API.Services;
using Smarticipate.Core.Entities;

namespace Smarticipate.API.Hubs;

public class SessionHub(
    LiveFeedbackStore feedbackStore,
    IServiceScopeFactory scopeFactory,
    IHubContext<SessionHub> hubContext)
    : Hub
{
    // Grace before a vanished teacher is reported gone: long enough to ride out a reload, a device switch, or a multi-minute network outage so connected students are not dropped while the teacher reconnects. The teacher client keeps retrying for the same window (see LiveSessionServices).
    private static readonly TimeSpan TeacherDisconnectGrace = TimeSpan.FromMinutes(3);

    private static readonly Dictionary<string, HashSet<string>> _teacherConnections = new();
    private static readonly Dictionary<string, HashSet<string>> _studentConnections = new();

    private static readonly Dictionary<string, (int QuestionId, int Duration, DateTime StartTime)> _activeQuestions =
        new();

    private readonly LiveFeedbackStore _feedbackStore = feedbackStore;
    private readonly IServiceScopeFactory _scopeFactory = scopeFactory;
    private readonly IHubContext<SessionHub> _hubContext = hubContext;

    private string? CurrentUserId => Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;

    // Authenticated owner of this session?
    private async Task<bool> OwnsSessionAsync(string sessionCode)
    {
        var userId = CurrentUserId;
        if (string.IsNullOrEmpty(userId)) return false;
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<UserDbContext>();
        return await db.Sessions.AnyAsync(s => s.SessionCode == sessionCode && s.UserId == userId);
    }

    private async Task EnsureOwnerAsync(string sessionCode)
    {
        if (!await OwnsSessionAsync(sessionCode))
            throw new HubException("Not authorized for this session.");
    }

    // Drop any in-flight question for a session so it cannot replay into a later run with the same code. Static so server-side termination (LiveSessionTerminator) can reach the in-memory state.
    public static void DropActiveQuestion(string sessionCode)
    {
        lock (_activeQuestions)
            _activeQuestions.Remove(sessionCode);
    }

    public async Task JoinSession(string sessionCode)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, sessionCode);
    }

    public async Task LeaveSession(string sessionCode)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, sessionCode);
    }

    public async Task EndSession(string sessionCode)
    {
        await EnsureOwnerAsync(sessionCode);

        // Drop any in-flight question so it can't replay into a later run with the same code.
        lock (_activeQuestions)
            _activeQuestions.Remove(sessionCode);

        // Final snapshot + clear live state + dismiss open questions + notify everyone.
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<UserDbContext>();
        await LiveSessionTerminator.EndAsync(db, _feedbackStore, _hubContext, sessionCode);
    }

    // Pulled by the overlay AFTER it subscribes, so the replay can't fire too early.
    // Always reflects live (currently-connected) students; no snapshot fallback, so an
    // empty session reads as 0 regardless of how it was (re)started.
    public async Task RequestTeacherState(string sessionCode)
    {
        await EnsureOwnerAsync(sessionCode);

        var agg = _feedbackStore.GetAggregate(sessionCode);

        using (var scope = _scopeFactory.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<UserDbContext>();

            var session = await db.Sessions
                .Include(s => s.StudentQuestions)
                .OrderByDescending(s => s.StartTime)
                .FirstOrDefaultAsync(s => s.SessionCode == sessionCode);

            // Replay open (non-dismissed) questions so the queue rebuilds
            if (session is not null)
            {
                foreach (var q in session.StudentQuestions
                             .Where(q => q.DismissedAt == null)
                             .OrderBy(q => q.CreatedAt))
                {
                    await Clients.Caller.SendAsync("QuestionReceived", q.Id, q.Text, q.CreatedAt);
                }
            }
        }

        await Clients.Caller.SendAsync("FeedbackUpdated", agg.PaceCounts, agg.UnderstandingCounts,
            agg.RespondentCount);
    }

    // The student sends both current slider values; this edits their existing (seeded) entry.
    public async Task UpdateFeedback(string sessionCode, int pace, int understanding)
    {
        _feedbackStore.Set(sessionCode, Context.ConnectionId, pace, understanding);
        var agg = _feedbackStore.GetAggregate(sessionCode);
        await Clients.Group($"teacher-{sessionCode}").SendAsync("FeedbackUpdated", agg.PaceCounts,
            agg.UnderstandingCounts, agg.RespondentCount);
    }

    // Teacher reset: keep everyone counted but snap them all back to neutral.
    public async Task ResetFeedback(string sessionCode)
    {
        await EnsureOwnerAsync(sessionCode);

        _feedbackStore.ResetToNeutral(sessionCode);

        // tells students to snap their overlays back to neutral
        await Clients.Group($"student-{sessionCode}").SendAsync("FeedbackReset");
        var agg = _feedbackStore.GetAggregate(sessionCode);
        await Clients.Group($"teacher-{sessionCode}").SendAsync("FeedbackUpdated",
            agg.PaceCounts, agg.UnderstandingCounts, agg.RespondentCount);
    }

    public async Task SubmitQuestion(string sessionCode, string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return;
        text = text.Trim();

        StudentQuestion question;
        using (var scope = _scopeFactory.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<UserDbContext>();
            var session = await db.Sessions
                .OrderByDescending(s => s.StartTime)
                .FirstOrDefaultAsync(s => s.SessionCode == sessionCode);
            if (session is null) return;
            question = new StudentQuestion
            {
                Text = text, SessionId = session.Id
            };
            db.StudentQuestions.Add(question);
            await db.SaveChangesAsync();
        }

        await Clients.Group($"teacher-{sessionCode}")
            .SendAsync("QuestionReceived", question.Id, question.Text, question.CreatedAt);
    }

    // Dismiss every open question for the session at once (used by "Start fresh").
    public async Task ClearQuestions(string sessionCode)
    {
        await EnsureOwnerAsync(sessionCode);

        using (var scope = _scopeFactory.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<UserDbContext>();
            var session = await db.Sessions
                .Include(s => s.StudentQuestions)
                .OrderByDescending(s => s.StartTime)
                .FirstOrDefaultAsync(s => s.SessionCode == sessionCode);
            if (session is null) return;

            var open = session.StudentQuestions.Where(q => q.DismissedAt == null).ToList();
            if (open.Count == 0) return;

            foreach (var q in open)
                q.DismissedAt = DateTime.Now;
            await db.SaveChangesAsync();

            foreach (var q in open)
                await Clients.Group($"teacher-{sessionCode}").SendAsync("QuestionDismissed", q.Id);
        }
    }

    public async Task DismissQuestion(string sessionCode, int questionId)
    {
        await EnsureOwnerAsync(sessionCode);

        using (var scope = _scopeFactory.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<UserDbContext>();
            var question = await db.StudentQuestions.FindAsync(questionId);
            if (question is null) return;
            question.DismissedAt = DateTime.Now;
            await db.SaveChangesAsync();
        }

        // Keep multiple teacher tabs in sync.
        await Clients.Group($"teacher-{sessionCode}").SendAsync("QuestionDismissed", questionId);
    }

    // A connection must belong to at most one session at a time. Switching sessions
    // (e.g. a teacher opening another session) has to drop the old group memberships,
    // otherwise broadcasts from the old session leak into the new view.
    private async Task LeaveOtherSessionsAsync(string connectionId, string keepSessionCode)
    {
        List<string> teacherCodes;
        lock (_teacherConnections)
        {
            teacherCodes = _teacherConnections
                .Where(kv => kv.Key != keepSessionCode && kv.Value.Contains(connectionId))
                .Select(kv => kv.Key).ToList();
        }

        List<string> studentCodes;
        lock (_studentConnections)
        {
            studentCodes = _studentConnections
                .Where(kv => kv.Key != keepSessionCode && kv.Value.Contains(connectionId))
                .Select(kv => kv.Key).ToList();
        }

        foreach (var code in teacherCodes)
        {
            await Groups.RemoveFromGroupAsync(connectionId, $"teacher-{code}");
            await Groups.RemoveFromGroupAsync(connectionId, code);
            lock (_teacherConnections)
            {
                if (_teacherConnections.TryGetValue(code, out var set))
                {
                    set.Remove(connectionId);
                    if (set.Count == 0) _teacherConnections.Remove(code);
                }
            }
        }

        foreach (var code in studentCodes)
        {
            await Groups.RemoveFromGroupAsync(connectionId, $"student-{code}");
            await Groups.RemoveFromGroupAsync(connectionId, code);
            _feedbackStore.Remove(code, connectionId);

            int remaining;
            lock (_studentConnections)
            {
                if (_studentConnections.TryGetValue(code, out var set))
                {
                    set.Remove(connectionId);
                    remaining = set.Count;
                    if (set.Count == 0) _studentConnections.Remove(code);
                }
                else remaining = 0;
            }

            // Tell the old session's teacher the student left and refresh its aggregate.
            await Clients.Group($"teacher-{code}").SendAsync("StudentCountChanged", remaining);
            var agg = _feedbackStore.GetAggregate(code);
            await Clients.Group($"teacher-{code}").SendAsync("FeedbackUpdated",
                agg.PaceCounts, agg.UnderstandingCounts, agg.RespondentCount);
        }
    }

    public async Task RegisterAsTeacher(string sessionCode)
    {
        await EnsureOwnerAsync(sessionCode);

        var connectionId = Context.ConnectionId;

        try
        {
            await LeaveOtherSessionsAsync(connectionId, sessionCode);

            // Add this connection to the teachers group for this session
            await Groups.AddToGroupAsync(connectionId, $"teacher-{sessionCode}");

            // Track this connection as a teacher for this session
            lock (_teacherConnections)
            {
                if (!_teacherConnections.ContainsKey(sessionCode))
                {
                    _teacherConnections[sessionCode] = new HashSet<string>();
                }

                _teacherConnections[sessionCode].Add(connectionId);
            }

            await Groups.AddToGroupAsync(connectionId, sessionCode);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"ERROR in RegisterAsTeacher: {ex.Message}");
        }
    }

    public async Task RegisterAsStudent(string sessionCode)
    {
        var connectionId = Context.ConnectionId;

        try
        {
            await LeaveOtherSessionsAsync(connectionId, sessionCode);

            await Groups.AddToGroupAsync(connectionId, $"student-{sessionCode}");
            await Groups.AddToGroupAsync(connectionId, sessionCode);

            int studentCount;
            lock (_studentConnections)
            {
                if (!_studentConnections.ContainsKey(sessionCode))
                {
                    _studentConnections[sessionCode] = new HashSet<string>();
                }

                _studentConnections[sessionCode].Add(connectionId);
                studentCount = _studentConnections[sessionCode].Count;
            }

            await Clients.Group($"teacher-{sessionCode}").SendAsync("StudentCountChanged", studentCount);

            // Count this student immediately at neutral; they edit their value by moving a slider.
            _feedbackStore.Seed(sessionCode, connectionId);
            var seededAgg = _feedbackStore.GetAggregate(sessionCode);
            await Clients.Group($"teacher-{sessionCode}").SendAsync("FeedbackUpdated",
                seededAgg.PaceCounts, seededAgg.UnderstandingCounts, seededAgg.RespondentCount);

            var activeQuestion = GetActiveQuestionForSession(sessionCode);
            if (activeQuestion.HasValue)
            {
                // Send active question details directly to the newly connected student
                await Clients.Caller.SendAsync("QuestionStarted",
                    activeQuestion.Value.QuestionId,
                    activeQuestion.Value.Duration,
                    activeQuestion.Value.RemainingTime);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"ERROR in RegisterAsStudent: {ex.Message}");
        }
    }

    public async Task LeaveAsStudent(string sessionCode)
    {
        var connectionId = Context.ConnectionId;
        await Groups.RemoveFromGroupAsync(connectionId, $"student-{sessionCode}");

        await Groups.RemoveFromGroupAsync(connectionId, sessionCode);

        int studentCount = 0;

        lock (_studentConnections)
        {
            if (_studentConnections.ContainsKey(sessionCode))
            {
                _studentConnections[sessionCode].Remove(connectionId);
                studentCount = _studentConnections[sessionCode].Count;

                if (_studentConnections[sessionCode].Count == 0)
                {
                    _studentConnections.Remove(sessionCode);
                }
            }
        }

        await Clients.Group($"teacher-{sessionCode}").SendAsync("StudentCountChanged", studentCount);

        // Also drop this student's live feedback and rebroadcast the aggregate.
        _feedbackStore.Remove(sessionCode, connectionId);
        var feedbackAgg = _feedbackStore.GetAggregate(sessionCode);
        await Clients.Group($"teacher-{sessionCode}").SendAsync("FeedbackUpdated",
            feedbackAgg.PaceCounts, feedbackAgg.UnderstandingCounts, feedbackAgg.RespondentCount);
    }

    public async Task StartQuestion(string sessionCode, int questionId, int duration)
    {
        await EnsureOwnerAsync(sessionCode);

        lock (_activeQuestions)
        {
            _activeQuestions[sessionCode] = (questionId, duration, DateTime.Now);
        }

        await Clients.Group(sessionCode).SendAsync("QuestionStarted", questionId, duration, duration);
    }

    public async Task StopQuestion(string sessionCode)
    {
        await EnsureOwnerAsync(sessionCode);

        lock (_activeQuestions)
        {
            _activeQuestions.Remove(sessionCode);
        }

        await Clients.Group(sessionCode).SendAsync("QuestionStopped");
    }

    private (int QuestionId, int Duration, int RemainingTime)? GetActiveQuestionForSession(string sessionCode)
    {
        lock (_activeQuestions)
        {
            if (_activeQuestions.TryGetValue(sessionCode, out var questionInfo))
            {
                //Check time passed since question started
                var elapsed = (int)(DateTime.Now - questionInfo.StartTime).TotalSeconds;
                var remaining = Math.Max(0, questionInfo.Duration - elapsed);

                return (questionInfo.QuestionId, questionInfo.Duration, remaining);
            }
        }

        return null;
    }

    public async Task UpdateRemainingTime(string sessionCode, int remainingTime)
    {
        await EnsureOwnerAsync(sessionCode);

        await Clients.Group(sessionCode).SendAsync("TimerUpdated", remainingTime);
    }

    // Keepalive no-op: the client invokes it periodically to keep the connection's
    // traffic alive; nothing needs to be sent back.
    public Task SendHeartbeat() => Task.CompletedTask;

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        // Check if this was a teacher connection
        string? teacherSession = null;

        lock (_teacherConnections)
        {
            foreach (var session in _teacherConnections)
            {
                if (session.Value.Contains(Context.ConnectionId))
                {
                    teacherSession = session.Key;
                    session.Value.Remove(Context.ConnectionId);

                    // If this was the last teacher connection for this session
                    if (session.Value.Count == 0)
                    {
                        _teacherConnections.Remove(teacherSession);
                        break;
                    }
                }
            }
        }

        // If this was the last teacher for a session, notify students — but only after a
        // grace window. A teacher reload drops the connection then re-registers within a
        // second or two; without the delay students get wrongly kicked with "Session ended".
        if (teacherSession != null && !_teacherConnections.ContainsKey(teacherSession))
        {
            var sessionKey = teacherSession;
            var hub = _hubContext;
            _ = Task.Run(async () =>
            {
                await Task.Delay(TeacherDisconnectGrace);
                bool stillGone;
                lock (_teacherConnections)
                    stillGone = !_teacherConnections.ContainsKey(sessionKey);
                if (stillGone)
                    await hub.Clients.Group(sessionKey).SendAsync("TeacherDisconnected");
            });
        }

        string? studentSession = null;
        int studentCount = 0;

        lock (_studentConnections)
        {
            foreach (var session in _studentConnections)
            {
                if (session.Value.Contains(Context.ConnectionId))
                {
                    studentSession = session.Key;
                    session.Value.Remove(Context.ConnectionId);
                    studentCount = session.Value.Count;

                    if (session.Value.Count == 0)
                    {
                        _studentConnections.Remove(studentSession);
                    }

                    break;
                }
            }
        }

        if (studentSession is not null)
        {
            await Clients.Group($"teacher-{studentSession}").SendAsync("StudentCountChanged", studentCount);
        }

        // Remove this connection's live feedback and rebroadcast the aggregate.
        var feedbackSession = _feedbackStore.RemoveConnectionEverywhere(Context.ConnectionId);
        if (feedbackSession is not null)
        {
            var agg = _feedbackStore.GetAggregate(feedbackSession);
            await Clients.Group($"teacher-{feedbackSession}").SendAsync("FeedbackUpdated",
                agg.PaceCounts, agg.UnderstandingCounts, agg.RespondentCount);
        }

        await base.OnDisconnectedAsync(exception);
    }
}