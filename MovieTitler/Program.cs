
using Microsoft.VisualBasic;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.Configuration;
using System.IO;
using System.Timers;
using Newtonsoft.Json;
using NLog;
using Topshelf;
using Tweetinvi;
using Tweetinvi.Events;
using Tweetinvi.Models;
using Tweetinvi.Streaming;
using System.Threading.Tasks;
using System.Reflection;
using System.Linq;
using System.Text.RegularExpressions;

public static class Program {

    public static void Main() {
        HostFactory.Run(x => {
            x.Service<MovieTitler>(s => {
                s.ConstructUsing(name => new MovieTitler());
                s.WhenStarted(tc =>tc.ServiceStart());
                s.WhenStopped(tc =>tc.ServiceStop());
            });

            x.RunAsLocalSystem();
            x.SetDescription("Twitter bot that combines movie titles and subtitles");
            x.SetDisplayName("Twitter Movie Titler");
            x.SetServiceName("MovieTitler");
        });
    }

}

public class MovieTitler {
    private static Logger logger = LogManager.GetCurrentClassLogger();

    private static Random R = new Random();
    private static Regex PARTX = new Regex("( -)? Part ([XVI]+|[1-9]+)$");

    // This task is launched when the class is initialized, and it creates everything below except TweetTimer.
    private Task InitTask;

    // A list of movie titles. Read from the SourceFile.
    private IReadOnlyList<string> FullTitles;

    // Titles and subtitles of movies.
    private IReadOnlyList<string> Titles;
    private IReadOnlyList<string> Subtitles;

    // The most recent tweets made by this account that were not replies. Used to avoid reusing a title/subtitle in a short amount of time.
    private List<string> PreviousTweets;
    private int PreviousTweetsToKeep;

    // Twitter credentials, read from KeysFile.
    private ITwitterCredentials Credentials;

    // Timer for periodically sending a tweet.
    private Timer TweetTimer;

    // User stream - used to read @replies so the bot can respond.
    private IUserStream UserStream;

    public MovieTitler() {
        logger.Debug("Creating tweet timer...");
        TweetTimer = new Timer();
        TweetTimer.Elapsed += SendTweet;

        // The rest of the class members are initialized asynchronously, so that the service can start quickly without having to request additional time from Windows.
        InitTask = Task.Run(() => InitTaskSubroutine());
    }

    private void InitTaskSubroutine() {
        logger.Debug("Reading text file...");
        string[] fileNames = ConfigurationManager.AppSettings["SourceFile"].Split(',');

        // Look in .exe folder first if the SourceFile given is just a filename, not a path
        for (int i = 0; i < fileNames.Length; i++) {
            if (Path.GetFileName(fileNames[i]) == fileNames[i]) {
                string location = Path.GetDirectoryName(Assembly.GetEntryAssembly().Location);
                fileNames[i] = Path.Combine(location, fileNames[i]);
            }
        }

        List<string> fullTitles = new List<string>();
        List<string> titles = new List<string>();
        List<string> subtitles = new List<string>();

        foreach (string fileName in fileNames) {
            string[] sourceFileContents = File.ReadAllLines(fileName);

            foreach (var line in sourceFileContents) {
                // If the line has a tab in it, only use text after the tab
                string[] split1 = line.Split('\t');
                if (split1.Length > 1) {
                    string fullTitle = split1[1];
                    if (fullTitles.Contains(fullTitle)) continue;
                    fullTitles.Add(fullTitle);

                    if (PARTX.IsMatch(fullTitle)) {
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
                    if (index >= 0) {
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
        }

        this.FullTitles = fullTitles;
        logger.Info("Found " + fullTitles.Count + " movies");

        this.Titles = titles.Distinct().ToList();
        logger.Info("Found " + this.Titles.Count + " titles");
        this.Subtitles = subtitles.Distinct().ToList();
        logger.Info("Found " + this.Subtitles.Count + " subtitles");

        PreviousTweetsToKeep = Math.Min(this.Titles.Count, this.Subtitles.Count) / 2;
        logger.Info("Will remember up to " + PreviousTweetsToKeep + " previous tweets (will try to avoid reusing parts of titles in that range)");

        // Credentials are stored in a json file.
        // Plain text or xml would have been fine too, but the Twitter integration means we need to include the json parser anyway.
        logger.Debug("Reading credentials...");
        var jsonObj = JsonConvert.DeserializeObject<Dictionary<string, string>>(File.ReadAllText(ConfigurationManager.AppSettings["KeysFile"]));

        Credentials = new TwitterCredentials(jsonObj["ConsumerKey"], jsonObj["ConsumerSecret"], jsonObj["AccessToken"], jsonObj["AccessTokenSecret"]);

        logger.Debug("Creating user stream...");
        UserStream = Tweetinvi.Stream.CreateUserStream(Credentials);

        // The Start() method may have already run - check whether the periodic tweet timer is running, and make sure the user stream has the same state.
        if (TweetTimer.Enabled) {
            logger.Debug("Tweet timer is already running - turning on user stream.");
            UserStream.StartStreamAsync();
        } else {
            logger.Debug("Tweet timer is not running - not turning on user stream yet.");
        }

        logger.Debug("Finding logged in user...");
        IUserIdentifier u = null;
        Auth.ExecuteOperationWithCredentials(Credentials, () => {
            u = User.GetAuthenticatedUser();
            if (u == null) {
                logger.Error(ExceptionHandler.GetLastException());
                return;
            }
        });

        logger.Debug("Getting previous tweets...");
        PreviousTweets = new List<string>();
        Auth.ExecuteOperationWithCredentials(Credentials, () => {
            if (u == null) {
                return;
            }

            var parameters = new Tweetinvi.Parameters.UserTimelineParameters();
            parameters.ExcludeReplies = true;
            parameters.MaximumNumberOfTweetsToRetrieve = PreviousTweetsToKeep;
            var tweets = Timeline.GetUserTimeline(u, parameters);
            foreach (var tweet in tweets) {
                if (!tweet.Text.StartsWith("@")) {
                    logger.Debug("Found previous tweet: " + tweet.Text);
                    this.PreviousTweets.Add(tweet.Text);
                }
            }
        });
    }

    public string Generate()
    {
        for (int i = 0; ; i++)
        {
            int index1 = R.Next(0, Titles.Count);
            int index2 = R.Next(0, Subtitles.Count);
            string part1 = Titles[index1];
            string part2 = Subtitles[index2];
            string newTitle = part1 + part2;

            if (FullTitles.Contains(newTitle))
            {
                // This is a real movie, so don't tweet it.
                continue;
            }

            if (i >= 50)
            {
                // Tried 50 combinations and they all result in reuse of part of a title - give up and use it anyway.
                return newTitle;
            }

            var similar = PreviousTweets.Where(s => s.StartsWith(part1) | s.EndsWith(part2));
            if (similar.Any())
            {
                // Either the title or subtitle was used in a recent tweet - try generating a new title.
                //logger.Info("Skipping generated title: " + newTitle);
                continue;
            }

            return newTitle;
        }
    }

    public void SendTweet(object state, EventArgs args) {
        try {
            InitTask.Wait();

            logger.Trace(DateTime.Now);

            TweetTimer.Interval = double.Parse(ConfigurationManager.AppSettings["IntervalMs"] ?? "60000");

            string newTitle = Generate();

            logger.Info(DateTime.Now + ": " + newTitle);
            PreviousTweets.Add(newTitle);
            if (PreviousTweets.Count > PreviousTweetsToKeep) {
                PreviousTweets.RemoveAt(0);
            }
            Auth.ExecuteOperationWithCredentials(Credentials, () => Tweet.PublishTweet(newTitle));
        } catch (Exception ex) {
            logger.Error(ex);
        }
    }

    public void ServiceStart() {
        logger.Info("Service starting");
        string startTime = ConfigurationManager.AppSettings["StartTime"];
        if (startTime != null) {
            DateTime startAt = DateTime.Today + TimeSpan.Parse(startTime);
            if (startAt < DateTime.Now) {
                startAt = startAt.AddDays(1);
            }
            logger.Info("Will run at: " + startAt);
            TweetTimer.Interval = (startAt - DateTime.Now).TotalMilliseconds;
        } else {
            TweetTimer.Interval = double.Parse(ConfigurationManager.AppSettings["IntervalMs"] ?? "60000");
        }
        TweetTimer.Start();

        if (UserStream != null) {
            UserStream.StartStream();
        }
    }

    public void ServiceStop() {
        logger.Info("Service stopping");
        TweetTimer.Stop();

        if (UserStream != null) {
            UserStream.StopStream();
        }
    }
}
