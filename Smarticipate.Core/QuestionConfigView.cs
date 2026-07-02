namespace Smarticipate.Core;

// Per-type scalar config, serialised into QuestionDefinition.ConfigJson (jsonb).
// Every field is optional; each handler reads only the fields it needs and applies defaults.
public sealed record QuestionConfigView
{
    public int? DefaultDurationSeconds { get; init; }

    // Scale
    public int? ScaleMin { get; init; }
    public int? ScaleMax { get; init; }
    public string? ScaleMinLabel { get; init; }
    public string? ScaleMaxLabel { get; init; }

    // Numeric
    public decimal? NumericMin { get; init; }
    public decimal? NumericMax { get; init; }
    public string? NumericUnit { get; init; }

    // FreeText
    public int? FreeTextMaxLength { get; init; }

    // MultipleChoice
    public int? MinSelections { get; init; }
    public int? MaxSelections { get; init; }

    // WordCloud (follow-up)
    public int? MaxWords { get; init; }
}
