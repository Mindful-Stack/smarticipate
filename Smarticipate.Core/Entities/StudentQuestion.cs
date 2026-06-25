namespace Smarticipate.Core.Entities;

public class StudentQuestion
{
    public int Id { get; set; }
    public string Text { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.Now;

    public DateTime? DismissedAt { get; set; }

    public int SessionId { get; set; }
    public Session Session { get; set; } = null!;
}