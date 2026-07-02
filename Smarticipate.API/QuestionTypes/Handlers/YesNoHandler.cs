using Smarticipate.Core;
using Smarticipate.Core.Entities;

namespace Smarticipate.API.QuestionTypes.Handlers;

public sealed class YesNoHandler : OptionSelectionHandler
{
    public override QuestionType Type => QuestionType.YesNo;

    public override void ValidateDefinition(QuestionDefinition definition)
    {
        // YesNo is stored as two auto-created options, so it must have exactly two.
        if (definition.Options.Count != 2)
            throw new QuestionValidationException("YesNo must have exactly two options.");
    }

    protected override (int Min, int Max) SelectionBounds(QuestionDefinition definition) => (1, 1);
}
