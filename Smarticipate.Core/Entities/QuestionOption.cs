namespace Smarticipate.Core.Entities;

public class QuestionOption
{
    public int Id { get; set; }
    public int DefinitionId { get; set; }
    public QuestionDefinition Definition { get; set; } = null!;

    public string Text { get; set; } = string.Empty;
    public int Ordinal { get; set; }

    // Reserved for quiz grading; unused in this work. Zero-cost forward provision.
    public bool IsCorrect { get; set; }
}
