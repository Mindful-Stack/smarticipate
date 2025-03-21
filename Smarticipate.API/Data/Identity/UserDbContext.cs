using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Smarticipate.API.Data.Identity;

public class UserDbContext(DbContextOptions<UserDbContext> options) : IdentityDbContext<IdentityUser>(options);