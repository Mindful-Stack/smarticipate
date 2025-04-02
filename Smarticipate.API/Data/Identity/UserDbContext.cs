using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Smarticipate.Core.Entities;

namespace Smarticipate.API.Data.Identity;

public class UserDbContext : IdentityDbContext<User>
{
    public DbSet<Session> Sessions { get; set; }
    public DbSet<Question> Questions { get; set; }
    public DbSet<Response> Responses { get; set; }
    public UserDbContext(DbContextOptions<UserDbContext> options) 
        : base(options)
    {
    }
}