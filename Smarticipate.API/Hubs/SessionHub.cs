using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Smarticipate.API.Data.Identity;
using Smarticipate.API.Services;
using Smarticipate.Core.Entities;

namespace Smarticipate.API.Hubs;

public class SessionHub(LiveFeedbackStore feedbackStore, IServiceScopeFactory scopeFactory)
    : Hub
{
    private static readonly Dictionary<string, HashSet<string>> _teacherConnections = new();
    private static readonly Dictionary<string, HashSet<string>> _studentConnections = new();

    private static readonly Dictionary<string, (int QuestionId, int Duration, DateTime StartTime)> _activeQuestions =
        new();

    private readonly LiveFeedbackStore _feedbackStore = feedbackStore;
    private readonly IServiceScopeFactory _scopeFactory = scopeFactory;

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
        // Persist a final aggregate, then clear the volatile state, then notify everyone
        using (var scope = _scopeFactory.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<UserDbContext>();
            await FeedbackSnapshotWriter.WriteAsync(db, _feedbackStore, sessionCode);
        }

        _feedbackStore.Reset(sessionCode);
        await Clients.Group(sessionCode).SendAsync("SessionEnded");
    }

    // Pulled by the overlay AFTER it subscribes, so the replay can't fire too early
    // allowSnapshotFallback: seed from the last snapshot only when there's no live data
    public async Task RequestTeacherState(string sessionCode, bool allowSnapshotFallback)
    {
        var agg = _feedbackStore.GetAggregate(sessionCode);

        using (var scope = _scopeFactory.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<UserDbContext>();

            var session = await db.Sessions
                .Include(s => s.StudentQuestions)
                .OrderByDescending(s => s.StartTime)
                .FirstOrDefaultAsync(s => s.SessionCode == sessionCode);

            if (session is not null)
            {
                // Replay open (non-dismissed) questions so the queue rebuilds
                foreach (var q in session.StudentQuestions
                             .Where(q => q.DismissedAt == null)
                             .OrderBy(q => q.CreatedAt))
                {
                    await Clients.Caller.SendAsync("QuestionReceived", q.Id, q.Text, q.CreatedAt);
                }

                // Resume fallback: only when there is no live data to show
                if (agg.RespondentCount == 0 && allowSnapshotFallback)
                {
                    var snap = await db.FeedbackSnapshots
                        .Where(fs => fs.SessionId == session.Id)
                        .OrderByDescending(fs => fs.Timestamp)
                        .FirstOrDefaultAsync();
                    if (snap is not null)
                        agg = new FeedbackAggregate(snap.PaceCounts, snap.UnderstandingCounts, snap.RespondentCount);
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

    public async Task DismissQuestion(string sessionCode, int questionId)
    {
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

    public async Task RegisterAsTeacher(string sessionCode)
    {
        var connectionId = Context.ConnectionId;

        try
        {
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
            Console.WriteLine($"Added {connectionId} to {sessionCode} general group");

            // Send a test message to verify the connection
            await Clients.Caller.SendAsync("Debug", "Teacher registration successful");
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
                Console.WriteLine(
                    $"Student IDs in session {sessionCode}: {string.Join(", ", _studentConnections[sessionCode])}");
            }

            await Clients.Group($"teacher-{sessionCode}").SendAsync("StudentCountChanged", studentCount);
            Console.WriteLine($"Sent StudentCountChanged({studentCount}) to teacher-{sessionCode}");

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
        lock (_activeQuestions)
        {
            _activeQuestions[sessionCode] = (questionId, duration, DateTime.Now);
        }

        await Clients.Group(sessionCode).SendAsync("QuestionStarted", questionId, duration, duration);
    }

    public async Task StopQuestion(string sessionCode)
    {
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
        await Clients.Group(sessionCode).SendAsync("TimerUpdated", remainingTime);
    }

    public async Task SendHeartbeat()
    {
        await Clients.Caller.SendAsync("Debug", "Heartbeat received");
    }

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

        // If this was the last teacher for a session, notify students
        if (teacherSession != null && !_teacherConnections.ContainsKey(teacherSession))
        {
            await Clients.Group(teacherSession).SendAsync("TeacherDisconnected");
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

        Console.WriteLine($"Student left session {studentSession}, new count: {studentCount}");

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