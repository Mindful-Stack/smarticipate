namespace Smarticipate.API.QuestionTypes;

// Thrown by handlers for malformed definitions or answers. Endpoints translate it to 400.
public sealed class QuestionValidationException(string message) : Exception(message);
