using Smarticipate.API.QuestionTypes;
using Smarticipate.API.QuestionTypes.Handlers;
using Smarticipate.Core;
using Smarticipate.Core.Entities;
using Xunit;

namespace Smarticipate.Tests.Handlers;

public class FreeTextHandlerTests
{
    private static QuestionActivation Activation() =>
        new() { Definition = new QuestionDefinition { Type = QuestionType.FreeText } };

    [Fact]
    public void PopulateResponse_TrimsAndStores()
    {
        var handler = new FreeTextHandler();
        var response = new Response();
        handler.PopulateResponse(Activation(), response, new ResponseInput(Text: "  hello  "));
        Assert.Equal("hello", response.TextValue);
        // Single-channel invariant: only the text channel is written.
        Assert.Null(response.NumericValue);
        Assert.Empty(response.Selections);
    }

    [Fact]
    public void PopulateResponse_RejectsEmpty()
    {
        var handler = new FreeTextHandler();
        Assert.Throws<QuestionValidationException>(() =>
            handler.PopulateResponse(Activation(), new Response(), new ResponseInput(Text: "   ")));
    }
}
