using Smarticipate.API.QuestionTypes;
using Smarticipate.API.QuestionTypes.Handlers;
using Smarticipate.Core;
using Smarticipate.Core.Entities;
using Xunit;

namespace Smarticipate.Tests.Handlers;

public class SingleChoiceHandlerTests
{
    private static QuestionActivation ActivationWithOptions(params int[] optionIds)
    {
        var definition = new QuestionDefinition
        {
            Type = QuestionType.SingleChoice,
            Options = optionIds.Select((id, i) => new QuestionOption { Id = id, Text = $"Opt{id}", Ordinal = i }).ToList()
        };
        return new QuestionActivation { Definition = definition };
    }

    [Fact]
    public void PopulateResponse_WritesSingleSelection()
    {
        var handler = new SingleChoiceHandler();
        var activation = ActivationWithOptions(10, 11);
        var response = new Response();

        handler.PopulateResponse(activation, response, new ResponseInput(OptionIds: [10]));

        Assert.Single(response.Selections);
        Assert.Equal(10, response.Selections[0].OptionId);
        Assert.Null(response.NumericValue);
        Assert.Null(response.TextValue);
    }

    [Fact]
    public void PopulateResponse_RejectsNumericOnlySubmission()
    {
        // An option type ignores the numeric channel; a numeric-only answer has no valid
        // selection, so it is rejected. This is how a wrong-channel submit manifests here.
        var handler = new SingleChoiceHandler();
        var activation = ActivationWithOptions(10, 11);

        Assert.Throws<QuestionValidationException>(() =>
            handler.PopulateResponse(activation, new Response(), new ResponseInput(Numeric: 3)));
    }

    [Fact]
    public void PopulateResponse_RejectsMoreThanOne()
    {
        var handler = new SingleChoiceHandler();
        var activation = ActivationWithOptions(10, 11);

        Assert.Throws<QuestionValidationException>(() =>
            handler.PopulateResponse(activation, new Response(), new ResponseInput(OptionIds: [10, 11])));
    }

    [Fact]
    public void PopulateResponse_RejectsForeignOption()
    {
        var handler = new SingleChoiceHandler();
        var activation = ActivationWithOptions(10, 11);

        Assert.Throws<QuestionValidationException>(() =>
            handler.PopulateResponse(activation, new Response(), new ResponseInput(OptionIds: [999])));
    }
}
