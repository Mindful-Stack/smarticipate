using System.Collections.Concurrent;

namespace Smarticipate.API.Services;

// Volatile in-memory live feedback for running sessions
// Singleton so the hub and the snapshot background service share one source of truth
public class LiveFeedbackStore
{
    public const int Steps = 5;

    // sessionCode -> (connectionId -> feedback)
    private readonly ConcurrentDictionary<string, ConcurrentDictionary<string, StudentFeedback>> _feedback = new();

    public record StudentFeedback(int? Pace, int? Understanding);

    public void Set(string sessionCode, string connectionId, int? pace, int? understanding)
    {
        if (pace is null && understanding is null) return;

        var session = _feedback.GetOrAdd(sessionCode, _ => new ConcurrentDictionary<string, StudentFeedback>());
        session.AddOrUpdate(
            connectionId,
            _ => new StudentFeedback(pace, understanding),
            (_, existing) => new StudentFeedback(pace ?? existing.Pace, understanding ?? existing.Understanding));
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

    public void Reset(string sessionCode) => _feedback.TryRemove(sessionCode, out _);

    public FeedbackAggregate GetAggregate(string sessionCode)
    {
        var pace = new int[Steps];
        var understanding = new int[Steps];
        var respondents = 0;

        if (_feedback.TryGetValue(sessionCode, out var session))
        {
            foreach (var fb in session.Values)
            {
                // Only count an axis the student has actually moved (null axes are skipped).
                if (fb.Pace is >= 1 and <= Steps) pace[fb.Pace.Value - 1]++;
                if (fb.Understanding is >= 1 and <= Steps) understanding[fb.Understanding.Value - 1]++;
                // Respondent = engaged at least one axis (true for every stored entry).
                if (fb.Pace is not null || fb.Understanding is not null) respondents++;
            }
        }

        return new FeedbackAggregate(pace, understanding, respondents);
    }

    public IReadOnlyCollection<string> ActiveSessions => _feedback.Keys.ToList();
}

public record FeedbackAggregate(int[] PaceCounts, int[] UnderstandingCounts, int RespondentCount);