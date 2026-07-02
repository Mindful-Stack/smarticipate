using Smarticipate.Core;
using Smarticipate.Core.Entities;

namespace Smarticipate.API.QuestionTypes.Handlers;

// A 1..5 rating by default; range is configurable but always integer steps.
public sealed class ScaleHandler : IQuestionTypeHandler
{
    private const int DefaultMin = 1;
    private const int DefaultMax = 5;

    public QuestionType Type => QuestionType.Scale;

    public void ValidateDefinition(QuestionDefinition definition)
    {
        var (min, max) = Range(definition);
        if (min >= max)
            throw new QuestionValidationException("Scale minimum must be below its maximum.");
    }

    public void PopulateResponse(QuestionActivation activation, Response response, ResponseInput input)
    {
        if (input.Numeric is not { } value)
            throw new QuestionValidationException("A rating is required.");

        var (min, max) = Range(activation.Definition);
        if (value < min || value > max || value != decimal.Truncate(value))
            throw new QuestionValidationException($"Rating must be a whole number between {min} and {max}.");

        response.NumericValue = value;
    }

    public QuestionResult Aggregate(QuestionActivation activation)
    {
        var (min, max) = Range(activation.Definition);
        var values = activation.Responses
            .Where(r => r.NumericValue.HasValue)
            .Select(r => r.NumericValue!.Value)
            .ToList();

        // Distribution across every integer step, so empty steps still render as zero.
        var distribution = Enumerable.Range(min, max - min + 1)
            .Select(step => new NumericBucket(step, values.Count(v => v == step)))
            .ToList();

        var stats = new NumericStats(
            values.Count == 0 ? 0 : Math.Round(values.Average(), 2),
            values.Count == 0 ? 0 : values.Min(),
            values.Count == 0 ? 0 : values.Max(),
            distribution);

        return QuestionResult.ForNumeric(activation, stats);
    }

    private static (int Min, int Max) Range(QuestionDefinition definition)
    {
        var config = definition.Config;
        return (config.ScaleMin ?? DefaultMin, config.ScaleMax ?? DefaultMax);
    }
}
