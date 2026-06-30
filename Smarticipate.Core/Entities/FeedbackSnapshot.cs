namespace Smarticipate.Core.Entities;

public class FeedbackSnapshot
{
    public int Id { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.Now;
    public int RespondentCount { get; set; }

    // How many students were sitting on each slider step at snapshot time.
    // Each array has 5 entries, one per step; the value is the headcount for that step.
    public int[] PaceCounts { get; set; } = new int[5];
    public int[] UnderstandingCounts { get; set; } = new int[5];

    public int SessionId { get; set; }
    public Session Session { get; set; } = null!;
}