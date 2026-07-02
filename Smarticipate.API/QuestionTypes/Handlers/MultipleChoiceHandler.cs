using Smarticipate.Core;
using Smarticipate.Core.Entities;

namespace Smarticipate.API.QuestionTypes.Handlers;

public sealed class MultipleChoiceHandler : OptionSelectionHandler
{
    public override QuestionType Type => QuestionType.MultipleChoice;

    protected override (int Min, int Max) SelectionBounds(QuestionDefinition definition)
    {
        var config = definition.Config;
        var optionCount = definition.Options.Count;
        var min = Math.Clamp(config.MinSelections ?? 1, 1, optionCount);
        var max = Math.Clamp(config.MaxSelections ?? optionCount, min, optionCount);
        return (min, max);
    }
}
