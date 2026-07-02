namespace Smarticipate.Core.Entities;

// One participant's answer to one activation. Exactly one channel is populated, decided
// by the definition's type and enforced by the type handler on the single submit path.
public class Response
{
    public int Id { get; set; }

    public int ActivationId { get; set; }
    public QuestionActivation Activation { get; set; } = null!;

    // Client-generated GUID. Anonymous to the teacher; used only for dedup and revise.
    public string ParticipantKey { get; set; } = string.Empty;

    public DateTime SubmittedAt { get; set; } = DateTime.UtcNow;

    // Scalar channels.
    public decimal? NumericValue { get; set; } // Scale, Numeric
    public string? TextValue { get; set; }     // FreeText
    // WordCloud's TextValues (text[]) is added in the follow-up PR (section 9.1); not present now.

    // Option channel: YesNo, SingleChoice, MultipleChoice, and (follow-up) Ranking.
    public List<ResponseSelection> Selections { get; set; } = [];
}
