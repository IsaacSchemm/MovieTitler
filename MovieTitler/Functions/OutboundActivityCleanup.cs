using System;
using System.Linq;
using System.Threading.Tasks;
using MovieTitler.Data;
using Microsoft.Azure.Functions.Worker;
using Microsoft.EntityFrameworkCore;

namespace MovieTitler.Functions
{
    public class OutboundActivityCleanup(BotDbContext context)
    {
        /// <summary>
        /// Removes pending outbound activities that are more than seven days
        /// old, and removes those inboxes from the list of known inboxes.
        /// Runs 2 minutes after the top of the hour.
        /// </summary>
        /// <param name="myTimer"></param>
        /// <returns></returns>
        [Function("OutboundActivityCleanup")]
        public async Task Run([TimerTrigger("0 2 * * * *")] TimerInfo myTimer)
        {
            var cutoff = DateTime.UtcNow - TimeSpan.FromDays(7);

            while (true)
            {
                var activities = await context.OutboundActivities
                    .Where(a => a.StoredAt < cutoff)
                    .OrderBy(a => a.StoredAt)
                    .Take(100)
                    .ToListAsync();

                if (activities.Count == 0)
                    break;

                var inboxes = activities.Select(a => a.Inbox).ToHashSet();

                var knownToRemove = await context.KnownInboxes
                    .Where(k => inboxes.Contains(k.Inbox))
                    .ToListAsync();

                context.KnownInboxes.RemoveRange(knownToRemove);
                context.OutboundActivities.RemoveRange(activities);

                await context.SaveChangesAsync();
            }
        }
    }
}
