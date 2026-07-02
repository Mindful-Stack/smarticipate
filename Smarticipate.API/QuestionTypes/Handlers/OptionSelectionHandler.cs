using Smarticipate.Core;
using Smarticipate.Core.Entities;

namespace Smarticipate.API.QuestionTypes.Handlers;

// Shared behaviour for the option-bearing types: YesNo, SingleChoice, MultipleChoice
// (and, in the follow-up, Ranking). Validates the selected options belong to the
// definition, enforces the per-type selection count, dedups, and tallies per option.
public abstract class OptionSelectionHandler : IQuestionTypeHandler
{
    public abstract QuestionType Type { get; }

    // Inclusive bounds on how many options a single answer may select.
    protected abstract (int Min, int Max) SelectionBounds(QuestionDefinition definition);

    public virtual void ValidateDefinition(QuestionDefinition definition)
    {
        if (definition.Options.Count < 2)
            throw new QuestionValidationException($"{Type} needs at least two options.");
    }

    public void PopulateResponse(QuestionActivation activation, Response response, ResponseInput input)
    {
        var definition = activation.Definition;
        var validIds = definition.Options.Select(o => o.Id).ToHashSet();

        // Dedup (review point 7): a client may repeat an option; count it once.
        var ids = (input.OptionIds ?? []).Distinct().ToList();

        if (ids.Count == 0)
            throw new QuestionValidationException("At least one option must be selected.");
        if (ids.Any(id => !validIds.Contains(id)))
            throw new QuestionValidationException("A selected option does not belong to this question.");

        var (min, max) = SelectionBounds(definition);
        if (ids.Count < min || ids.Count > max)
            throw new QuestionValidationException(
                min == max
                    ? $"Select exactly {min} option(s)."
                    : $"Select between {min} and {max} options.");

        response.Selections = ids
            .Select(id => new ResponseSelection { OptionId = id, Ordinal = 0 })
            .ToList();
    }

    public QuestionResult Aggregate(QuestionActivation activation)
    {
        var definition = activation.Definition;
        var counts = definition.Options.ToDictionary(o => o.Id, _ => 0);

        foreach (var selection in activation.Responses.SelectMany(r => r.Selections))
            if (counts.ContainsKey(selection.OptionId))
                counts[selection.OptionId]++;

        var tallies = definition.Options
            .OrderBy(o => o.Ordinal)
            .Select(o => new OptionTally(o.Id, o.Text, o.Ordinal, counts[o.Id]))
            .ToList();

        return QuestionResult.ForOptions(activation, tallies);
    }
}
