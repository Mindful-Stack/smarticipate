using Smarticipate.Core;
using Smarticipate.Core.Entities;

namespace Smarticipate.API.QuestionTypes;

public interface IQuestionTypeHandler
{
    QuestionType Type { get; }

    // Options and config are coherent for this type. Throws QuestionValidationException if not.
    void ValidateDefinition(QuestionDefinition definition);

    // Validates the input for this type and writes exactly one channel onto response.
    // The caller passes a response that has been reset (scalars null, selections cleared).
    // activation.Definition and its Options must be loaded.
    void PopulateResponse(QuestionActivation activation, Response response, ResponseInput input);

    // Shapes the collected responses into a result. activation.Responses (with Selections)
    // and activation.Definition.Options must be loaded.
    QuestionResult Aggregate(QuestionActivation activation);
}
