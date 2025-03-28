using System.Reflection;

namespace Smarticipate.API.Endpoints;

public static class IEndpointRouteBuilderExtensions
{
    public static void MapEndpoints<T>(this IApplicationBuilder app)
    {
        MapEndpoints(app, typeof(T));
    }
    
    public static void MapEndpoints(this IApplicationBuilder app, Type T)
    {
        var endpointTypes = GetEndpointTypesFromAssemblyContaining(T);

        foreach (var endpointType in endpointTypes)
        {
            endpointType.GetMethod(nameof(IEndpoint.MapEndpoint))!
                .Invoke(null, new object[] { app });
        }
    }

    private static IEnumerable<TypeInfo> GetEndpointTypesFromAssemblyContaining(Type typeMarker)
    {
        var endpointTypes = typeMarker.Assembly.DefinedTypes
            .Where(x => !x.IsAbstract && !x.IsInterface &&
                        typeof(IEndpoint).IsAssignableFrom(x));
        return endpointTypes;
    }
}