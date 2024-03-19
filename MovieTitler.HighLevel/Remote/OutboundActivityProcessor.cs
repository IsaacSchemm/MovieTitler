using MovieTitler.Data;
using Microsoft.EntityFrameworkCore;

namespace MovieTitler.HighLevel.Remote
{
    /// <summary>
    /// Sends queued ActivityPub activities to other servers.
    /// </summary>
    public class OutboundActivityProcessor(BotDbContext context, Requester requester)
    {
        /// <summary>
        /// Sends queued ActivityPub activities to other servers.
        /// </summary>
        /// <remarks>
        /// If an outbox cannot be reached, the message - and any further
        /// activities to that inbox - will be skipped for the next four
        /// hours.
        /// </remarks>
        public async Task ProcessOutboundActivities()
        {
            HashSet<string> inboxesToSkip = [];

            while (true)
            {
                var activities = await context.OutboundActivities
                    .Where(a => !inboxesToSkip.Contains(a.Inbox))
                    .OrderBy(a => a.StoredAt)
                    .Take(100)
                    .ToListAsync();

                if (activities.Count == 0)
                    return;

                foreach (var activity in activities)
                {
                    // If this activity is to be skipped, also skip any other activity to the same inbox
                    if (activity.DelayUntil > DateTimeOffset.UtcNow)
                        inboxesToSkip.Add(activity.Inbox);

                    // If we're now skipping this inbox, skip this activity
                    if (inboxesToSkip.Contains(activity.Inbox))
                        continue;

                    try
                    {
                        await requester.SendAsync(activity);
                        context.OutboundActivities.Remove(activity);
                    }
                    catch (HttpRequestException)
                    {
                        // Don't send this activity again for four hours
                        // This will also skip later activities to that inbox (see above)
                        activity.DelayUntil = DateTimeOffset.UtcNow.AddHours(4);
                        inboxesToSkip.Add(activity.Inbox);
                    }

                    await context.SaveChangesAsync();
                }
            }
        }
    }
}
