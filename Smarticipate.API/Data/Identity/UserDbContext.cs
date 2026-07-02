using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Smarticipate.Core.Entities;

namespace Smarticipate.API.Data.Identity;

public class UserDbContext : IdentityDbContext<User>
{
    public DbSet<Session> Sessions { get; set; }
    public DbSet<QuestionDefinition> QuestionDefinitions { get; set; }
    public DbSet<QuestionOption> QuestionOptions { get; set; }
    public DbSet<QuestionActivation> QuestionActivations { get; set; }
    public DbSet<Response> Responses { get; set; }
    public DbSet<ResponseSelection> ResponseSelections { get; set; }
    public DbSet<FeedbackSnapshot> FeedbackSnapshots { get; set; }
    public DbSet<StudentQuestion> StudentQuestions { get; set; }

    public UserDbContext(DbContextOptions<UserDbContext> options)
        : base(options)
    {
    }

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<QuestionDefinition>(e =>
        {
            e.Property(d => d.ConfigJson).HasColumnType("jsonb");

            // OwnerUserId is a real FK to the Identity user (design section 4), with an index for the
            // toolbox filter. Restrict, not cascade: user deletion is not a feature here, and a cascade
            // from User would collide with the Restrict on Definition -> Activation. Null rows (system
            // definitions) are unaffected by the FK.
            e.HasOne<User>()
                .WithMany()
                .HasForeignKey(d => d.OwnerUserId)
                .OnDelete(DeleteBehavior.Restrict);
            e.HasIndex(d => d.OwnerUserId);

            e.HasMany(d => d.Options)
                .WithOne(o => o.Definition)
                .HasForeignKey(o => o.DefinitionId)
                .OnDelete(DeleteBehavior.Cascade);

            // Definitions are never row-deleted (delete = un-save), so block cascade from here.
            e.HasMany(d => d.Activations)
                .WithOne(a => a.Definition)
                .HasForeignKey(a => a.DefinitionId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<QuestionActivation>(e =>
        {
            // Deleting a session removes its activations (matches existing session-delete cascade).
            e.HasOne(a => a.Session)
                .WithMany(s => s.Activations)
                .HasForeignKey(a => a.SessionId)
                .OnDelete(DeleteBehavior.Cascade);

            e.HasMany(a => a.Responses)
                .WithOne(r => r.Activation)
                .HasForeignKey(r => r.ActivationId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<Response>(e =>
        {
            // One response per participant per activation; revise = upsert.
            e.HasIndex(r => new { r.ActivationId, r.ParticipantKey }).IsUnique();

            e.HasMany(r => r.Selections)
                .WithOne(s => s.Response)
                .HasForeignKey(s => s.ResponseId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<ResponseSelection>(e =>
        {
            // Belt-and-suspenders dedup alongside the handler Distinct (review point 7).
            e.HasIndex(s => new { s.ResponseId, s.OptionId }).IsUnique();

            // Keep historical selections intact; block deleting an option that has been chosen.
            e.HasOne(s => s.Option)
                .WithMany()
                .HasForeignKey(s => s.OptionId)
                .OnDelete(DeleteBehavior.Restrict);
        });
    }
}
