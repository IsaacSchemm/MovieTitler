using Microsoft.EntityFrameworkCore;

namespace MovieTitler.Data
{
    public class MovieTitlerDbContext(DbContextOptions<MovieTitlerDbContext> options) : DbContext(options)
    {
        public DbSet<Follower> Followers { get; set; }
        public DbSet<GeneratedPost> GeneratedPosts { get; set; }
    }
}
