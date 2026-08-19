using Microsoft.EntityFrameworkCore;
using PollBuilder.Voting.Models;

namespace PollBuilder.Voting.Data
{
    /// <summary>
    /// EF Core context for VotingService. It owns the <c>voting</c> schema; PollService neither reads
    /// nor writes it. Both schemas live on the same Neon instance to stay inside the free tier, which is
    /// a deliberate cost trade-off rather than shared ownership of the data.
    /// </summary>
    public class VotingDbContext : DbContext
    {
        public VotingDbContext(DbContextOptions<VotingDbContext> options) : base(options) { }

        /// <summary>Schema this service owns.</summary>
        public const string Schema = "voting";

        public DbSet<Vote> Votes => Set<Vote>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            ArgumentNullException.ThrowIfNull(modelBuilder);
            base.OnModelCreating(modelBuilder);

            // SQLite, which the integration tests run against, has no concept of schemas.
            if (Database.IsNpgsql())
            {
                modelBuilder.HasDefaultSchema(Schema);
            }

            modelBuilder.Entity<Vote>(entity =>
            {
                entity.HasKey(v => v.Id);
                entity.Property(v => v.PollCode).HasMaxLength(16).IsRequired();
                entity.Property(v => v.VoterToken).HasMaxLength(64).IsRequired();

                // The one-vote-per-respondent rule. Enforcing it here means two simultaneous requests
                // from the same browser cannot both pass an application-level "have you voted?" check.
                entity.HasIndex(v => new { v.PollCode, v.VoterToken }).IsUnique();

                // Results are always grouped by option within a poll.
                entity.HasIndex(v => new { v.PollCode, v.OptionIndex });
            });
        }
    }
}
