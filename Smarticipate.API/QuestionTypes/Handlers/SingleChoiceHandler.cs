using Smarticipate.Core;
using Smarticipate.Core.Entities;

namespace Smarticipate.API.QuestionTypes.Handlers;

public sealed class SingleChoiceHandler : OptionSelectionHandler
{
    public override QuestionType Type => QuestionType.SingleChoice;
    protected override (int Min, int Max) SelectionBounds(QuestionDefinition definition) => (1, 1);
}
