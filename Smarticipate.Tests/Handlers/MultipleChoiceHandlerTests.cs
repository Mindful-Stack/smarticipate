using Smarticipate.API.QuestionTypes;
using Smarticipate.API.QuestionTypes.Handlers;
using Smarticipate.Core;
using Smarticipate.Core.Entities;
using Xunit;

namespace Smarticipate.Tests.Handlers;

public class MultipleChoiceHandlerTests
{
    private static QuestionActivation Activation(QuestionConfigView? config = null)
    {
        var definition = new QuestionDefinition
        {
            Type = QuestionType.MultipleChoice,
            Options =
            [
                new QuestionOption { Id = 1, Text = "A", Ordinal = 0 },
                new QuestionOption { Id = 2, Text = "B", Ordinal = 1 },
                new QuestionOption { Id = 3, Text = "C", Ordinal = 2 }
            ]
        };
        if (config is not null) definition.Config = config;
        return new QuestionActivation { Definition = definition };
    }

    [Fact]
    public void PopulateResponse_DedupsRepeatedOption()
    {
        var handler = new MultipleChoiceHandler();
        var response = new Response();

        handler.PopulateResponse(Activation(), response, new ResponseInput(OptionIds: [1, 1, 2]));

        Assert.Equal(2, response.Selections.Count);
        Assert.Equal([1, 2], response.Selections.Select(s => s.OptionId).OrderBy(x => x));
        // Single-channel invariant: only the option channel is written.
        Assert.Null(response.NumericValue);
        Assert.Null(response.TextValue);
    }

    [Fact]
    public void PopulateResponse_EnforcesMaxSelections()
    {
        var handler = new MultipleChoiceHandler();
        var activation = Activation(new QuestionConfigView { MaxSelections = 1 });

        Assert.Throws<QuestionValidationException>(() =>
            handler.PopulateResponse(activation, new Response(), new ResponseInput(OptionIds: [1, 2])));
    }
}
