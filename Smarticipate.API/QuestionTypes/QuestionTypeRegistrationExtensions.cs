namespace Smarticipate.API.QuestionTypes;

public static class QuestionTypeRegistrationExtensions
{
    public static IServiceCollection AddQuestionTypeHandlers(this IServiceCollection services)
    {
        var handlerTypes = typeof(QuestionTypeRegistrationExtensions).Assembly.DefinedTypes
            .Where(t => t is { IsAbstract: false, IsInterface: false }
                        && typeof(IQuestionTypeHandler).IsAssignableFrom(t));

        foreach (var t in handlerTypes)
            services.AddSingleton(typeof(IQuestionTypeHandler), t);

        services.AddSingleton<QuestionTypeRegistry>();
        return services;
    }
}
