using Microsoft.AspNetCore.Identity;
using Smarticipate.Core.Entities;

namespace Smarticipate.API.Data.Identity;

public class User : IdentityUser
{
    public List<Session> Sessions { get; set; } = new();
}