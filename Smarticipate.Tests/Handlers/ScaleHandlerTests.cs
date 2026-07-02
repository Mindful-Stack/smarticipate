using Smarticipate.API.QuestionTypes;
using Smarticipate.API.QuestionTypes.Handlers;
using Smarticipate.Core;
using Smarticipate.Core.Entities;
using Xunit;

namespace Smarticipate.Tests.Handlers;

public class ScaleHandlerTests
{
    private static QuestionActivation Activation() =>
        new() { Definition = new QuestionDefinition { Type = QuestionType.Scale } };

    [Fact]
    public void PopulateResponse_AcceptsInRange()
    {
        var handler = new ScaleHandler();
        var response = new Response();
        handler.PopulateResponse(Activation(), response, new ResponseInput(Numeric: 4));
        Assert.Equal(4, response.NumericValue);
        // Single-channel invariant: only the numeric channel is written.
        Assert.Null(response.TextValue);
        Assert.Empty(response.Selections);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(6)]
    [InlineData(2.5)]
    public void PopulateResponse_RejectsOutOfRangeOrFractional(double value)
    {
        var handler = new ScaleHandler();
        Assert.Throws<QuestionValidationException>(() =>
            handler.PopulateResponse(Activation(), new Response(), new ResponseInput(Numeric: (decimal)value)));
    }

    [Fact]
    public void Aggregate_ProducesFullDistribution()
    {
        var handler = new ScaleHandler();
        var activation = Activation();
        activation.Responses =
        [
            new Response { NumericValue = 5 },
            new Response { NumericValue = 5 },
            new Response { NumericValue = 3 }
        ];

        var result = handler.Aggregate(activation);

        Assert.NotNull(result.Numeric);
        Assert.Equal(5, result.Numeric!.Distribution.Count); // steps 1..5 all present
        Assert.Equal(2, result.Numeric.Distribution.Single(b => b.Value == 5).Count);
        Assert.Equal(0, result.Numeric.Distribution.Single(b => b.Value == 1).Count);
    }
}
