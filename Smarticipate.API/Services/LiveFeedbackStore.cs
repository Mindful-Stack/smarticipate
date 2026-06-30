using System.Collections.Concurrent;

namespace Smarticipate.API.Services;

// Volatile in-memory live feedback for running sessions
// Singleton so the hub and the snapshot background service share one source of truth
public class LiveFeedbackStore
{
    public const int Steps = 5;
    public const int Neutral = (Steps + 1) / 2; // middle step (3) — every student starts here

    // sessionCode -> (connectionId -> feedback)
    private readonly ConcurrentDictionary<string, ConcurrentDictionary<string, StudentFeedback>> _feedback = new();

    public record StudentFeedback(int Pace, int Understanding);

    // Seed a newly-joined student at neutral so connected-but-silent students are still counted.
    public void Seed(string sessionCode, string connectionId)
        => Set(sessionCode, connectionId, Neutral, Neutral);

    // Upsert keyed by connectionId, so moving a slider EDITS the student's value (never adds).
    public void Set(string sessionCode, string connectionId, int pace, int understanding)
    {
        var session = _feedback.GetOrAdd(sessionCode, _ => new ConcurrentDictionary<string, StudentFeedback>());
        session[connectionId] = new StudentFeedback(pace, understanding);
    }

    public void Remove(string sessionCode, string connectionId)
    {
        if (_feedback.TryGetValue(sessionCode, out var session))
            session.TryRemove(connectionId, out _);
    }

    // Removes a connection from whichever session holds it. returns that session code (if any)
    public string? RemoveConnectionEverywhere(string connectionId)
    {
        foreach (var (code, session) in _feedback)
        {
            if (session.TryRemove(connectionId, out _))
                return code;
        }

        return null;
    }

    // Full clear — used when a session ends.
    public void Reset(string sessionCode) => _feedback.TryRemove(sessionCode, out _);

    // Teacher "reset feedback": keep everyone connected but snap them all back to neutral.
    public void ResetToNeutral(string sessionCode)
    {
        if (_feedback.TryGetValue(sessionCode, out var session))
            foreach (var connId in session.Keys)
                session[connId] = new StudentFeedback(Neutral, Neutral);
    }

    public FeedbackAggregate GetAggregate(string sessionCode)
    {
        var pace = new int[Steps];
        var understanding = new int[Steps];
        var respondents = 0;

        if (_feedback.TryGetValue(sessionCode, out var session))
        {
            foreach (var fb in session.Values)
            {
                if (fb.Pace is >= 1 and <= Steps) pace[fb.Pace - 1]++;
                if (fb.Understanding is >= 1 and <= Steps) understanding[fb.Understanding - 1]++;
                respondents++; // every connected student counts (seeded at neutral)
            }
        }

        return new FeedbackAggregate(pace, understanding, respondents);
    }

    public IReadOnlyCollection<string> ActiveSessions => _feedback.Keys.ToList();
}

public record FeedbackAggregate(int[] PaceCounts, int[] UnderstandingCounts, int RespondentCount);