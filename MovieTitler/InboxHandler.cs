using MovieTitler.Data;
using MovieTitler.HighLevel;
using MovieTitler.HighLevel.Remote;
using MovieTitler.LowLevel;
using Microsoft.EntityFrameworkCore;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace MovieTitler
{
    /// <summary>
    /// Provides a way for the inbox handler to make changes in the bot's database.
    /// </summary>
    public class InboxHandler(ActivityPubTranslator translator, BotDbContext context)
    {
        /// <summary>
        /// Adds an actor's shared inbox (or personal inbox, if there is none
        /// to the bot's list of known inboxes, unless it is already present.
        /// </summary>
        /// <remarks>
        /// Inboxes that no longer exist will be removed by OutboundActivityCleanup.
        /// </remarks>
        /// <param name="actor">The ActivityPub actor to add</param>
        public async Task AddKnownInboxAsync(RemoteActor actor)
        {
            string personalInbox = actor.Inbox;
            string primaryInbox = actor.SharedInbox ?? actor.Inbox;

            var known = await context.KnownInboxes
                .Where(i => i.Inbox == personalInbox || i.Inbox == primaryInbox)
                .Take(1)
                .ToListAsync();

            if (known.Count == 0)
            {
                context.KnownInboxes.Add(new KnownInbox
                {
                    Id = Guid.NewGuid(),
                    Inbox = primaryInbox
                });
                await context.SaveChangesAsync();
            }
        }

        /// <summary>
        /// Adds a follower to the database. If the follower already exists,
        /// the ID of the Follow activity will be updated.
        /// </summary>
        /// <param name="objectId">The ID of the Follow activity, so Undo requests can be honored</param>
        /// <param name="actor">The follower to add</param>
        /// <returns></returns>
        public async Task AddFollowAsync(string objectId, RemoteActor actor)
        {
            var existing = await context.Followers
                .Where(f => f.ActorId == actor.Id)
                .SingleOrDefaultAsync();

            if (existing != null)
            {
                existing.MostRecentFollowId = objectId;
            }
            else
            {
                context.Followers.Add(new Follower
                {
                    Id = Guid.NewGuid(),
                    ActorId = actor.Id,
                    MostRecentFollowId = objectId,
                    Inbox = actor.Inbox,
                    SharedInbox = actor.SharedInbox
                });

                context.OutboundActivities.Add(new OutboundActivity
                {
                    Id = Guid.NewGuid(),
                    Inbox = actor.Inbox,
                    JsonBody = ActivityPubSerializer.SerializeWithContext(
                        translator.AcceptFollow(objectId)),
                    StoredAt = DateTimeOffset.UtcNow
                });
            }

            await context.SaveChangesAsync();
        }

        /// <summary>
        /// Remove a follower.
        /// </summary>
        /// <param name="objectId">The ID of the Follow activity</param>
        /// <returns></returns>
        public async Task RemoveFollowAsync(string objectId)
        {
            var followers = context.Followers
                .Where(i => i.MostRecentFollowId == objectId)
                .AsAsyncEnumerable();
            await foreach (var i in followers)
                context.Followers.Remove(i);

            await context.SaveChangesAsync();
        }
    }
}
