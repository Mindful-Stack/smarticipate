namespace Smarticipate.API.QuestionTypes;

// Normalised answer payload handed to a handler. Exactly one channel is meaningful per type;
// the handler validates and writes the correct channel onto the Response entity.
public sealed record ResponseInput(
    decimal? Numeric = null,
    string? Text = null,
    string[]? Words = null,
    // Order is significant for Ranking; a plain set for the other choice types.
    IReadOnlyList<int>? OptionIds = null
);
