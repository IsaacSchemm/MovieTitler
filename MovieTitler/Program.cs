using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Host;
using Microsoft.Extensions.Logging;

namespace FunctionApp1
{
    public static class Program
    {
        private static async Task<IEnumerable<string>> GetFileAsync(string url)
        {
            var req = WebRequest.CreateHttp(url);
            using (var resp = await req.GetResponseAsync())
            using (var sr = new StreamReader(resp.GetResponseStream()))
            {
                string text = await sr.ReadToEndAsync();
                return text.Split('\n').Where(x => !string.IsNullOrWhiteSpace(x));
            }
        }

        public class TitlesObject
        {
            public IList<string> FullTitles { get; set; }
            public IList<string> Titles { get; set; }
            public IList<string> Subtitles { get; set; }
        }

        private static Regex PARTX = new Regex("( -)? Part ([XVI]+|[1-9]+)$");

        public static async Task<TitlesObject> GetMoviesAsync()
        {
            var all = await Task.WhenAll(new[]
            {
                "https://raw.githubusercontent.com/IsaacSchemm/MovieTitler/master/MovieTitler/movie-titles.txt",
                "https://raw.githubusercontent.com/IsaacSchemm/MovieTitler/master/MovieTitler/best-picture-nominees.txt"
            }.Select(GetFileAsync));
            var distinct = all.SelectMany(s => s).Distinct();

            List<string> fullTitles = new List<string>();
            List<string> titles = new List<string>();
            List<string> subtitles = new List<string>();

            foreach (var line in distinct)
            {
                // If the line has a tab in it, only use text after the tab
                string[] split1 = line.Split('\t');
                if (split1.Length > 1)
                {
                    string fullTitle = split1[1];
                    if (fullTitles.Contains(fullTitle)) continue;
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
                    if (index == -1) index = fullTitle.LastIndexOf(" and the ");
                    if (index >= 0)
                    {
                        string title = fullTitle.Substring(0, index);
                        string subtitle = fullTitle.Substring(index);

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

        public static string Generate(TitlesObject obj)
        {
            var R = new Random();
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

                if (i >= 50)
                {
                    // Tried 50 combinations and they all result in reuse of part of a title - give up and use it anyway.
                    return newTitle;
                }

                return newTitle;
            }
        }

        [FunctionName("Function1")]
        public static async Task Run([TimerTrigger("0 0 5/8 * * *")]TimerInfo myTimer, ILogger log)
        {
            try
            {
                var obj = await GetMoviesAsync();
                string newTitle = Generate(obj);
                await Task.WhenAll(PostToMastodon(newTitle), PostToTwitter(newTitle));
            } catch (Exception ex)
            {
                log.LogError(ex, "Could not post");
            }
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
