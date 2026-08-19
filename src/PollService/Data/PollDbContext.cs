using Microsoft.EntityFrameworkCore;
using PollBuilder.Polls.Models;

namespace PollBuilder.Polls.Data
{
    /// <summary>
    /// EF Core context for PollService. It owns the <c>polls</c> schema only: no other service reads or
    /// writes these tables, which is what keeps the service boundary real rather than decorative.
    /// </summary>
    public class PollDbContext : DbContext
    {
        public PollDbContext(DbContextOptions<PollDbContext> options) : base(options) { }

        /// <summary>Schema this service owns. VotingService owns a separate one on the same Neon instance.</summary>
        public const string Schema = "polls";

        public DbSet<Poll> Polls => Set<Poll>();

        public DbSet<PollOption> PollOptions => Set<PollOption>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            ArgumentNullException.ThrowIfNull(modelBuilder);
            base.OnModelCreating(modelBuilder);

            // SQLite, which the integration tests run against, has no concept of schemas.
            if (Database.IsNpgsql())
            {
                modelBuilder.HasDefaultSchema(Schema);
            }

            modelBuilder.Entity<Poll>(entity =>
            {
                entity.HasKey(p => p.Id);
                entity.Property(p => p.Code).HasMaxLength(16).IsRequired();
                entity.Property(p => p.Question).HasMaxLength(300).IsRequired();
                entity.Property(p => p.CreatorTokenHash).HasMaxLength(64).IsRequired();
                entity.Property(p => p.Status).HasConversion<int>();

                // Share links resolve by code, so it must be unique and indexed.
                entity.HasIndex(p => p.Code).IsUnique();

                entity.HasMany(p => p.Options)
                      .WithOne(o => o.Poll)
                      .HasForeignKey(o => o.PollId)
                      .OnDelete(DeleteBehavior.Cascade);
            });

            modelBuilder.Entity<PollOption>(entity =>
            {
                entity.HasKey(o => o.Id);
                entity.Property(o => o.Text).HasMaxLength(200).IsRequired();
                entity.HasIndex(o => new { o.PollId, o.OptionIndex }).IsUnique();
            });
        }
    }
}
