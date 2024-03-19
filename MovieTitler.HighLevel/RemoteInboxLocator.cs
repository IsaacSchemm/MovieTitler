using MovieTitler.Data;

namespace MovieTitler.HighLevel
{
    public class RemoteInboxLocator(BotDbContext context)
    {
        /// <summary>
        /// Gets the URLs of known inboxes.
        /// </summary>
        /// <param name="followersOnly">Whether to limit the set to only followers' servers. If false, the bot will include all known inboxes.</param>
        /// <returns>A set of inbox URLs</returns>
        public async Task<IReadOnlySet<string>> GetDistinctInboxesAsync(bool followersOnly = false)
        {
            HashSet<string> inboxes = [];

            // Go through follower inboxes first - prefer shared inbox if present
            await foreach (var follower in context.Followers.AsAsyncEnumerable())
                inboxes.Add(follower.SharedInbox ?? follower.Inbox);

            // Then include all other known inboxes, if enabled
            if (!followersOnly)
                await foreach (var known in context.KnownInboxes.AsAsyncEnumerable())
                    inboxes.Add(known.Inbox);

            return inboxes;
        }
    }
}
