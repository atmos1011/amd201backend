using Microsoft.EntityFrameworkCore;
using PollManage.Models;

namespace PollManage.Data
{
    public class myContext : DbContext
    {
        public myContext(DbContextOptions<myContext> c) : base(c) { }

        public DbSet<Poll> Polls { get; set; }

        public DbSet<PollOption> PollOptions { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Share links are looked up by Code, so two polls must never have the same one.
            modelBuilder.Entity<Poll>().HasIndex(p => p.Code).IsUnique();

            // Deleting a poll deletes its options with it.
            modelBuilder.Entity<Poll>()
                .HasMany(p => p.Options)
                .WithOne(o => o.Poll)
                .HasForeignKey(o => o.PollId)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }
}
