using Smarticipate.Core;
using Smarticipate.Core.Entities;

namespace Smarticipate.API.QuestionTypes;

// Shaped aggregation returned by handler.Aggregate and served by the results endpoint.
// Only the section relevant to the type is populated; the rest are null.
public sealed record QuestionResult(
    QuestionType Type,
    int ActivationId,
    int ResponseCount,
    IReadOnlyList<OptionTally>? Options = null,
    NumericStats? Numeric = null,
    IReadOnlyList<string>? TextAnswers = null,
    IReadOnlyList<WordTally>? Words = null
)
{
    public static QuestionResult ForOptions(QuestionActivation a, IReadOnlyList<OptionTally> tallies) =>
        new(a.Definition.Type, a.Id, a.Responses.Count, Options: tallies);

    public static QuestionResult ForNumeric(QuestionActivation a, NumericStats stats) =>
        new(a.Definition.Type, a.Id, a.Responses.Count, Numeric: stats);

    public static QuestionResult ForText(QuestionActivation a, IReadOnlyList<string> answers) =>
        new(a.Definition.Type, a.Id, a.Responses.Count, TextAnswers: answers);
}

public sealed record OptionTally(int OptionId, string Text, int Ordinal, int Count);

public sealed record NumericStats(
    decimal Average,
    decimal Min,
    decimal Max,
    IReadOnlyList<NumericBucket> Distribution
);

public sealed record NumericBucket(decimal Value, int Count);

// Follow-up (WordCloud).
public sealed record WordTally(string Word, int Count);
