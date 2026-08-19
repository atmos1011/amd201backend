using Microsoft.EntityFrameworkCore;
using VoteManage.Models;

namespace VoteManage.Data
{
    public class myContext : DbContext
    {
        public myContext(DbContextOptions<myContext> c) : base(c) { }

        public DbSet<Vote> Votes { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // This is what really stops one browser voting twice. Checking in C# first is not
            // enough, because two clicks at the same time can both pass the check.
            modelBuilder.Entity<Vote>()
                .HasIndex(v => new { v.PollCode, v.VoterToken })
                .IsUnique();
        }
    }
}
