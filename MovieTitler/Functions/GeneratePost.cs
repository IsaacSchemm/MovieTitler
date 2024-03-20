using MovieTitler.Data;
using Microsoft.Azure.Functions.Worker;
using System.Text.RegularExpressions;
using MovieTitler.Generation;
using Microsoft.EntityFrameworkCore;
using MovieTitler.LowLevel;
using MovieTitler.HighLevel;
using Microsoft.FSharp.Collections;

namespace MovieTitler.Functions
{
    public partial class GeneratePost(ActivityPubTranslator translator, BotDbContext context, RemoteInboxLocator inboxLocator)
    {
        public record TitlesObject(
            FSharpSet<string> Titles,
            FSharpSet<string> Subtitles);

        private static readonly Regex PARTX = PartPattern();

        private static readonly Lazy<TitlesObject> Candidates = new(() =>
        {
            var all = MovieModule.FilterByRelevancy(
                Movies.List,
                most_recent_year_max: 100,
                other_years_max: 50);

            FSharpSet<string> titles = SetModule.Empty<string>();
            FSharpSet<string> subtitles = SetModule.Empty<string>();

            foreach (var movie in all)
            {
                string fullTitle = movie.Title;

                if (PARTX.IsMatch(fullTitle))
                {
                    string newTitle = PARTX.Replace(fullTitle, "");
                    fullTitle = newTitle;
                }

                // Get title/subtitle (if applicable) - look for last occurence of colon+space or space+dash+space
                int index = new[] {
                    fullTitle.LastIndexOf(" - "),
                    fullTitle.LastIndexOf(": ")
                }.Max();

                if (index == -1)
                    index = fullTitle.LastIndexOf(" and the ");

                if (index >= 0)
                {
                    string title = fullTitle[..index];
                    string subtitle = fullTitle[index..];

                    if (title == "Mission" && subtitle.StartsWith("Impossible"))
                        continue;
                    if (title == "Sex" && subtitle.StartsWith("City"))
                        continue;
                    if (title == "Beauty" && subtitle.StartsWith("Beast"))
                        continue;

                    titles = titles.Add(title);
                    subtitles = subtitles.Add(subtitle);
                }
            }

            return new TitlesObject(
                Titles: titles,
                Subtitles: subtitles);
        });

        private static readonly Random R = new();

        private static IEnumerable<string> Generate(TitlesObject obj)
        {
            while (true)
            {
                int index1 = R.Next(0, obj.Titles.Count);
                int index2 = R.Next(0, obj.Subtitles.Count);
                string part1 = obj.Titles.ElementAt(index1);
                string part2 = obj.Subtitles.ElementAt(index2);
                yield return part1 + part2;
            }
        }

        /// <summary>
        /// Creates and sends a new post. Runs every day at 06:00 UTC.
        /// </summary>
        /// <param name="myTimer"></param>
        /// <returns></returns>
        [Function("GeneratePost")]
        public async Task Run([TimerTrigger("0 0 6 * * *")] TimerInfo myTimer)
        {
            var recentPosts = await context.GeneratedPosts
                .OrderByDescending(post => post.Id)
                .Take(90)
                .ToListAsync();

            var recentContent = recentPosts
                .Select(post => post.Content);

            string newTitle = Generate(Candidates.Value)
                .Except(Movies.Titles)
                .Except(recentContent)
                .First();

            int previousId = recentPosts
                .Select(post => post.Id)
                .DefaultIfEmpty(0)
                .Max();

            var newPost = new GeneratedPost
            {
                Id = previousId + 1,
                Content = newTitle,
                CreatedAt = DateTimeOffset.UtcNow
            };

            context.GeneratedPosts.Add(newPost);

            string createActivityJson = ActivityPubSerializer.SerializeWithContext(
                translator.ObjectToCreate(
                    Domain.AsPost(
                        newPost)));

            foreach (string inbox in await inboxLocator.GetDistinctInboxesAsync())
            {
                context.OutboundActivities.Add(new OutboundActivity
                {
                    Id = Guid.NewGuid(),
                    Inbox = inbox,
                    JsonBody = createActivityJson,
                    StoredAt = DateTimeOffset.UtcNow
                });
            }

            await context.SaveChangesAsync();
        }

        [GeneratedRegex("( -)? Part ([XVI]+|[1-9]+)$")]
        private static partial Regex PartPattern();
    }
}
