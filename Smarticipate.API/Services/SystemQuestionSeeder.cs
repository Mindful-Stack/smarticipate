using Microsoft.EntityFrameworkCore;
using Smarticipate.API.Data.Identity;
using Smarticipate.Core;
using Smarticipate.Core.Entities;

namespace Smarticipate.API.Services;

public static class SystemQuestionSeeder
{
    private const string ReadyCheckName = "Ready check";

    public static async Task SeedAsync(UserDbContext db)
    {
        var exists = await db.QuestionDefinitions
            .AnyAsync(d => d.OwnerUserId == null && d.Name == ReadyCheckName);
        if (exists) return;

        db.QuestionDefinitions.Add(new QuestionDefinition
        {
            Type = QuestionType.YesNo,
            Prompt = "Are you ready?",
            Name = ReadyCheckName,
            IsSaved = true,
            OwnerUserId = null,
            CreatedAt = DateTime.UtcNow,
            Options =
            [
                new QuestionOption { Text = "Yes", Ordinal = 0 },
                new QuestionOption { Text = "No", Ordinal = 1 }
            ]
        });

        await db.SaveChangesAsync();
    }
}
