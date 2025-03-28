using Microsoft.EntityFrameworkCore;
using Smarticipate.API.Data.Identity;
using Smarticipate.Core.Entities;

namespace Smarticipate.API.Data;

public class SmarticipateApiDbContext : DbContext
{
    public DbSet<Session> Sessions { get; set; }
    public DbSet<Question> Questions { get; set; }
    public DbSet<Response> Responses { get; set; }

    public SmarticipateApiDbContext(DbContextOptions<SmarticipateApiDbContext> options): base(options)
    {        
    }

    // protected override void OnModelCreating(ModelBuilder modelBuilder)
    // {
    //     base.OnModelCreating(modelBuilder);
    //     
    //     modelBuilder.Entity<Session>()
    //         .HasOne<User>()
    //         .WithMany(u => u.Sessions)
    //         .HasForeignKey("UserId")
    //         .OnDelete(DeleteBehavior.Cascade);
    //     
    //     // Configure Question relationship with Session
    //     modelBuilder.Entity<Question>()
    //         .HasOne(q => q.Session)
    //         .WithMany(s => s.Questions)
    //         .HasForeignKey(q => q.SessionId)
    //         .OnDelete(DeleteBehavior.Cascade);
    //     
    //     // Configure Response relationship with Question
    //     modelBuilder.Entity<Response>()
    //         .HasOne(r => r.Question)
    //         .WithMany(q => q.Responses)
    //         .HasForeignKey(r => r.QuestionId)
    //         .OnDelete(DeleteBehavior.Cascade);
    //     
    // }
}