using System.ComponentModel.DataAnnotations.Schema;

namespace Smarticipate.Core.Entities;

public class Session
{
    public int Id { get; set; }
    public string SessionCode { get; set; }
    public DateTime? StartTime { get; set; } = DateTime.Now;
    public DateTime? EndTime { get; set; }
    
    // public int UserId { get; set; }
    // [ForeignKey("UserId")] 
    // public User User { get; set; } = null!;

    public List<Question> Questions { get; set; } = new();
}