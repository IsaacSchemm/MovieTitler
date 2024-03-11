using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.Azure.Functions.Worker;
using MovieTitler.Generation;

namespace FunctionApp1
{
    public partial class Function
    {
        public class TitlesObject
        {
            public IReadOnlyList<string> Titles { get; set; }
            public IReadOnlyList<string> Subtitles { get; set; }
        }

        private static readonly Regex PARTX = PartPattern();

        private static readonly Lazy<TitlesObject> Candidates = new(() =>
        {
            var all = MovieModule.FilterByRelevancy(
                Movies.List,
                most_recent_year_max: 100,
                other_years_max: 50);

            HashSet<string> titles = [];
            HashSet<string> subtitles = [];

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

                    titles.Add(title);
                    subtitles.Add(subtitle);
                }
            }

            return new TitlesObject
            {
                Titles = [.. titles],
                Subtitles = [.. subtitles]
            };
        });

        private static readonly Random R = new();

        private static IEnumerable<string> Generate(TitlesObject obj)
        {
            while (true)
            {
                int index1 = R.Next(0, obj.Titles.Count);
                int index2 = R.Next(0, obj.Subtitles.Count);
                string part1 = obj.Titles[index1];
                string part2 = obj.Subtitles[index2];
                yield return part1 + part2;
            }
        }

        [Function("Function1")]
        public async Task Run([TimerTrigger("0 0 5/8 * * *")]TimerInfo myTimer)
        {
            var obj = Candidates.Value;
            string newTitle = Generate(obj)
                .Except(Movies.Titles)
                .First();
            await Task.WhenAll(PostToMastodon(newTitle), PostToTwitter(newTitle));
        }

        private static async Task PostToMastodon(string s)
        {
            await Mastodon.Api.Statuses.Posting(
                Environment.GetEnvironmentVariable("MastodonDomain"),
                Environment.GetEnvironmentVariable("MastodonAccessToken"),
                s);
        }

        private static async Task PostToTwitter(string s)
        {
            Tweetinvi.Auth.SetUserCredentials(
                Environment.GetEnvironmentVariable("TwitterConsumerKey"),
                Environment.GetEnvironmentVariable("TwitterConsumerSecret"),
                Environment.GetEnvironmentVariable("TwitterTokenKey"),
                Environment.GetEnvironmentVariable("TwitterTokenSecret"));

            await Tweetinvi.TweetAsync.PublishTweet(s);
        }

        [GeneratedRegex("( -)? Part ([XVI]+|[1-9]+)$")]
        private static partial Regex PartPattern();
    }
}
