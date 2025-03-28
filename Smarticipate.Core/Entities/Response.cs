using System.ComponentModel.DataAnnotations.Schema;

namespace Smarticipate.Core.Entities;

public class Response
{
 public int Id { get; set; }
 public int SelectedOption { get; set; }
 public DateTime TimeStamp { get; set; } = DateTime.Now;

 public int QuestionId { get; set; }
 public Question Question { get; set; } = null!;
}