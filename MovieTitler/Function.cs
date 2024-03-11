using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.Azure.Functions.Worker;

namespace FunctionApp1
{
    public class Function(IHttpClientFactory httpClientFactory)
    {
        private async Task<IEnumerable<string>> GetFileAsync(string url)
        {
            using var client = httpClientFactory.CreateClient();
            using var resp = await client.GetAsync(url);
            resp.EnsureSuccessStatusCode();
            string text = await resp.Content.ReadAsStringAsync();
            return text.Split('\n').Where(x => !string.IsNullOrWhiteSpace(x));
        }

        public class TitlesObject
        {
            public IReadOnlyList<string> FullTitles { get; set; }
            public IReadOnlyList<string> Titles { get; set; }
            public IReadOnlyList<string> Subtitles { get; set; }
        }

        private static readonly Regex PARTX = new("( -)? Part ([XVI]+|[1-9]+)$");

        public async Task<TitlesObject> GetMoviesAsync()
        {
            var all = await GetFileAsync("https://raw.githubusercontent.com/IsaacSchemm/MovieTitler/master/MovieTitler/movie-titles.txt");

            List<string> fullTitles = [];

            List<string> titles = [];
            List<string> subtitles = [];

            foreach (var line in all)
            {
                // If the line has a tab in it, only use text after the tab
                string[] split1 = line.Split('\t');
                if (split1.Length > 1)
                {
                    string fullTitle = split1[1];
                    if (fullTitles.Contains(fullTitle))
                        continue;

                    fullTitles.Add(fullTitle);

                    if (PARTX.IsMatch(fullTitle))
                    {
                        string newTitle = PARTX.Replace(fullTitle, "");
                        fullTitle = newTitle;
                    }

                    // Get title/subtitle (if applicable) - look for last occurence of colon+space or space+dash+space
                    int index = new[] {
                        fullTitle.LastIndexOf(" ("),
                        fullTitle.LastIndexOf(" - "),
                        fullTitle.LastIndexOf(": ")
                    }.Max();

                    if (index == -1)
                        index = fullTitle.LastIndexOf(" and the ");

                    if (index >= 0)
                    {
                        string title = fullTitle[..index];
                        string subtitle = fullTitle[index..];

                        // Don't parse "Mission: Impossible" as a title and subtitle
                        if (title == "Mission") continue;
                        // Don't parse "Sex and the City" as two titles like Captain Holt does
                        if (title == "Sex") continue;
                        // Others to skip
                        if (title == "Il Postino") continue;
                        if (title == "Beauty") continue;

                        titles.Add(title);
                        subtitles.Add(subtitle);
                    }
                }
            }

            return new TitlesObject
            {
                FullTitles = fullTitles,
                Titles = titles.Distinct().ToList(),
                Subtitles = subtitles.Distinct().ToList()
            };
        }

        private static readonly Random R = new();

        private static string Generate(TitlesObject obj)
        {
            for (int i = 0; ; i++)
            {
                int index1 = R.Next(0, obj.Titles.Count);
                int index2 = R.Next(0, obj.Subtitles.Count);
                string part1 = obj.Titles[index1];
                string part2 = obj.Subtitles[index2];
                string newTitle = part1 + part2;

                if (obj.FullTitles.Contains(newTitle))
                {
                    // This is a real movie, so don't tweet it.
                    continue;
                }

                return newTitle;
            }
        }

        [Function("Function1")]
        public async Task Run([TimerTrigger("0 0 5/8 * * *")]TimerInfo myTimer)
        {
            var obj = await GetMoviesAsync();
            string newTitle = Generate(obj);
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
    }
}
