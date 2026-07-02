using Smarticipate.Core;

namespace Smarticipate.API.QuestionTypes;

public sealed class QuestionTypeRegistry
{
    private readonly IReadOnlyDictionary<QuestionType, IQuestionTypeHandler> _handlers;

    public QuestionTypeRegistry(IEnumerable<IQuestionTypeHandler> handlers)
    {
        _handlers = handlers.ToDictionary(h => h.Type);
    }

    public IQuestionTypeHandler For(QuestionType type) =>
        _handlers.TryGetValue(type, out var handler)
            ? handler
            : throw new QuestionValidationException($"Question type {type} is not supported yet.");

    public bool Supports(QuestionType type) => _handlers.ContainsKey(type);
}
