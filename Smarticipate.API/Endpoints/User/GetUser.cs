using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Smarticipate.API.Data.Identity;

namespace Smarticipate.API.Endpoints.User;

public class GetUser : IEndpoint
{
    public static void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapGet("api/users/{email}", async (string email, [FromServices] UserDbContext db) =>
            {
                var user = await db.Users.FirstOrDefaultAsync(u => u.Email == email);
                if (user == null)
                    return Results.NotFound();
                return Results.Ok(user.Id);
            })
            .WithTags("Users")
            .WithName("Get User by Email")
            .WithOpenApi()
            .Produces<string>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status404NotFound);
    }
}