namespace Smarticipate.API.Endpoints;

public interface IEndpoint
{
    public static abstract void MapEndpoint(IEndpointRouteBuilder app);
}