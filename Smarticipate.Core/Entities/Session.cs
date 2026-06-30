namespace Smarticipate.Core.Entities;

public class Session
{
    public int Id { get; set; }
    public string SessionCode { get; set; }
    public string? Name { get; set; }
    public DateTime? StartTime { get; set; } = DateTime.Now;
    public DateTime? EndTime { get; set; }
    public string? UserId { get; set; }

    public List<Question> Questions { get; set; } = new();
    public List<FeedbackSnapshot> FeedbackSnapshots { get; set; } = [];
    public List<StudentQuestion> StudentQuestions { get; set; } = [];
}