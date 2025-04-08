namespace Smarticipate.Core.Entities;

public class Question
{
    public int Id { get; set; }
    public int QuestionNumber { get; set; }
    public DateTime StartTime { get; set; } = DateTime.Now;
    public DateTime? EndTime { get; set; } = null;

    public int SessionId { get; set; }
    public Session Session { get; set; } = null!;

    public List<Response> Responses { get; set; } = new();
}