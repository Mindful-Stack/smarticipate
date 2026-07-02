using Smarticipate.Core;
using Smarticipate.Core.Entities;

namespace Smarticipate.API.QuestionTypes.Handlers;

public sealed class FreeTextHandler : IQuestionTypeHandler
{
    private const int DefaultMaxLength = 1000;

    public QuestionType Type => QuestionType.FreeText;

    public void ValidateDefinition(QuestionDefinition definition)
    {
        // Nothing structural to validate; a prompt is enough (checked at create time).
    }

    public void PopulateResponse(QuestionActivation activation, Response response, ResponseInput input)
    {
        var text = input.Text?.Trim();
        if (string.IsNullOrEmpty(text))
            throw new QuestionValidationException("A written answer is required.");

        var maxLength = activation.Definition.Config.FreeTextMaxLength ?? DefaultMaxLength;
        if (text.Length > maxLength)
            throw new QuestionValidationException($"Answer must be {maxLength} characters or fewer.");

        response.TextValue = text;
    }

    public QuestionResult Aggregate(QuestionActivation activation)
    {
        var answers = activation.Responses
            .Where(r => !string.IsNullOrEmpty(r.TextValue))
            .OrderBy(r => r.SubmittedAt)
            .Select(r => r.TextValue!)
            .ToList();

        return QuestionResult.ForText(activation, answers);
    }
}
