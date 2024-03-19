using Microsoft.EntityFrameworkCore;

namespace MovieTitler.Data
{
    public class BotDbContext(DbContextOptions<BotDbContext> options) : DbContext(options)
    {
        public DbSet<Follower> Followers { get; set; }
        public DbSet<GeneratedPost> GeneratedPosts { get; set; }
        public DbSet<KnownInbox> KnownInboxes { get; set; }
        public DbSet<OutboundActivity> OutboundActivities { get; set; }
    }
}
