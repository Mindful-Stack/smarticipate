using Smarticipate.Core;
using Smarticipate.Core.Entities;

namespace Smarticipate.API.QuestionTypes.Handlers;

// A free numeric answer, optionally bounded by config. No fixed distribution (open range).
public sealed class NumericHandler : IQuestionTypeHandler
{
    public QuestionType Type => QuestionType.Numeric;

    public void ValidateDefinition(QuestionDefinition definition)
    {
        var config = definition.Config;
        if (config.NumericMin is { } min && config.NumericMax is { } max && min > max)
            throw new QuestionValidationException("Numeric minimum must not exceed its maximum.");
    }

    public void PopulateResponse(QuestionActivation activation, Response response, ResponseInput input)
    {
        if (input.Numeric is not { } value)
            throw new QuestionValidationException("A number is required.");

        var config = activation.Definition.Config;
        if (config.NumericMin is { } min && value < min)
            throw new QuestionValidationException($"Number must be at least {min}.");
        if (config.NumericMax is { } max && value > max)
            throw new QuestionValidationException($"Number must be at most {max}.");

        response.NumericValue = value;
    }

    public QuestionResult Aggregate(QuestionActivation activation)
    {
        var values = activation.Responses
            .Where(r => r.NumericValue.HasValue)
            .Select(r => r.NumericValue!.Value)
            .ToList();

        // Group identical values into buckets; open range, so no fixed step set.
        var distribution = values
            .GroupBy(v => v)
            .OrderBy(g => g.Key)
            .Select(g => new NumericBucket(g.Key, g.Count()))
            .ToList();

        var stats = new NumericStats(
            values.Count == 0 ? 0 : Math.Round(values.Average(), 2),
            values.Count == 0 ? 0 : values.Min(),
            values.Count == 0 ? 0 : values.Max(),
            distribution);

        return QuestionResult.ForNumeric(activation, stats);
    }
}
