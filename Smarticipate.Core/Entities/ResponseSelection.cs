namespace Smarticipate.Core.Entities;

public class ResponseSelection
{
    public int Id { get; set; }

    public int ResponseId { get; set; }
    public Response Response { get; set; } = null!;

    public int OptionId { get; set; }
    public QuestionOption Option { get; set; } = null!;

    // Position, used by (follow-up) Ranking; 0 for single and multiple choice.
    public int Ordinal { get; set; }
}
