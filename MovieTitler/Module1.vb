Imports System.Configuration
Imports System.IO
Imports System.Timers
Imports Newtonsoft.Json
Imports NLog
Imports Topshelf
Imports Tweetinvi
Imports Tweetinvi.Events
Imports Tweetinvi.Models
Imports Tweetinvi.Streaming

Module Module1

    Sub Main()
        '' Create a New set of credentials for the application.
        'Dim appCredentials = New TwitterCredentials(ConfigurationManager.AppSettings("ConsumerKey"),
        '                                     ConfigurationManager.AppSettings("ConsumerSecret"))

        '' Init the authentication process And store the related `AuthenticationContext`.
        'Dim authenticationContext = AuthFlow.InitAuthentication(appCredentials)

        '' Go to the URL so that Twitter authenticates the user And gives them a PIN code.
        'Process.Start("firefox", authenticationContext.AuthorizationURL)

        '' Ask the user to enter the pin code given by Twitter
        'Dim pinCode = Console.ReadLine()

        '' With this pin code it Is now possible to get the credentials back from Twitter
        'Dim userCredentials = AuthFlow.CreateCredentialsFromVerifierCode(pinCode, authenticationContext)

        '' Use the user credentials in your application
        'Auth.SetCredentials(userCredentials)

        'Return

        HostFactory.Run(Sub(x)
                            x.Service(Of MovieTitler)(Sub(s)
                                                          s.ConstructUsing(Function(name)
                                                                               Return New MovieTitler()
                                                                           End Function)
                                                          s.WhenStarted(Sub(tc)
                                                                            tc.ServiceStart()
                                                                        End Sub)
                                                          s.WhenStopped(Sub(tc)
                                                                            tc.ServiceStop()
                                                                        End Sub)
                                                      End Sub)

                            x.RunAsLocalSystem
                            x.SetDescription("Twitter bot that combines movie titles and subtitles")
                            x.SetDisplayName("Twitter Movie Titler")
                            x.SetServiceName("MovieTitler")
                        End Sub)
    End Sub

End Module

Public Class MovieTitler
    Private Shared logger As Logger = LogManager.GetCurrentClassLogger()
    Private Shared R As Random = New Random()

    ' This task is launched when the class is initialized, and it creates everything below except TweetTimer.
    Private InitTask As Task

    ' A list of movie titles. Read from the SourceFile.
    Private FullTitles As IReadOnlyList(Of String)

    ' Titles and subtitles of movies.
    Private Titles As IReadOnlyList(Of String)
    Private Subtitles As IReadOnlyList(Of String)

    ' The 30 most recent tweets made by this account that were not replies. Used to avoid reusing a title/subtitle in a short amount of time.
    Private PreviousTweets As List(Of String)

    ' Twitter credentials, read from KeysFile.
    Private Credentials As ITwitterCredentials

    ' Timer for periodically sending a tweet.
    Private TweetTimer As Timer

    ' Twitter user ID of the account this bot is using.
    Private MyId As Long

    ' User stream - used to read @replies so the bot can respond.
    Private UserStream As IUserStream

    ' Limit of replies per minute, to avoid any possible problems from tweeting too often.
    Private ReplyLimit As Integer

    Public Sub New()
        logger.Debug("Creating tweet timer...")
        TweetTimer = New Timer()
        AddHandler TweetTimer.Elapsed, AddressOf SendTweet

        ' The rest of the class members are initialized asynchronously, so that the service can start quickly without having to request additional time from Windows.
        InitTask = Task.Run(
            Sub()
                logger.Debug("Reading text file...")
                Dim sourceFileContents = File.ReadAllLines(ConfigurationManager.AppSettings("SourceFile"))
                Dim fullTitles As New List(Of String)
                Dim titles As New List(Of String)
                Dim subtitles As New List(Of String)
                For Each fullTitle In sourceFileContents
                    ' If the line has a tab in it, only use text after the tab
                    Dim split1 = fullTitle.Split(vbTab)
                    If split1.Length > 1 Then
                        fullTitle = split1(1)
                        fullTitles.Add(fullTitle)

                        ' Get title/subtitle (if applicable) - look for last occurence of colon+space or space+dash+space
                        Dim index = Math.Max(fullTitle.LastIndexOf(" - "), fullTitle.LastIndexOf(": "))
                        If index >= 0 Then
                            Dim title = fullTitle.Substring(0, index)
                            Dim subtitle = fullTitle.Substring(index)
                            ' Don't parse "Mission: Impossible" as a title and subtitle
                            If title IsNot "Mission" Then
                                titles.Add(title)
                                subtitles.Add(subtitle)
                            End If
                        End If
                    End If
                Next

                Me.FullTitles = fullTitles
                logger.Info("Found " & fullTitles.Count & " movies")

                Me.Titles = titles.Distinct().ToList()
                logger.Info("Found " & Me.Titles.Count & " titles")
                Me.Subtitles = subtitles.Distinct().ToList()
                logger.Info("Found " & Me.Subtitles.Count & " subtitles")

                ' Credentials are stored in a json file.
                ' Plain text or xml would have been fine too, but the Twitter integration means we need to include the json parser anyway.
                logger.Debug("Reading credentials...")
                Dim jsonObj = JsonConvert.DeserializeObject(Of Dictionary(Of String, String))(File.ReadAllText(ConfigurationManager.AppSettings("KeysFile")))

                Credentials = New TwitterCredentials(jsonObj("ConsumerKey"),
                                         jsonObj("ConsumerSecret"),
                                         jsonObj("AccessToken"),
                                         jsonObj("AccessTokenSecret"))

                logger.Debug("Creating user stream...")
                ReplyLimit = 6
                UserStream = Tweetinvi.Stream.CreateUserStream(Credentials)
                AddHandler UserStream.TweetCreatedByAnyoneButMe, AddressOf ReplyHandler

                ' The Start() method may have already run - check whether the periodic tweet timer is running, and make sure the user stream has the same state.
                If TweetTimer.Enabled Then
                    logger.Debug("Tweet timer is already running - turning on user stream.")
                    UserStream.StartStreamAsync()
                Else
                    logger.Debug("Tweet timer is not running - not turning on user stream yet.")
                End If

                logger.Debug("Finding logged in user...")
                Dim u As IUserIdentifier = Nothing
                Auth.ExecuteOperationWithCredentials(Credentials, Sub()
                                                                      u = User.GetAuthenticatedUser()
                                                                      If u Is Nothing Then
                                                                          logger.Error(ExceptionHandler.GetLastException())
                                                                          Return
                                                                      End If
                                                                      Me.MyId = u.Id
                                                                  End Sub)

                logger.Debug("Getting previous tweets...")
                PreviousTweets = New List(Of String)
                Auth.ExecuteOperationWithCredentials(Credentials, Sub()
                                                                      If u Is Nothing Then
                                                                          Return
                                                                      End If

                                                                      Dim params As New Parameters.UserTimelineParameters
                                                                      params.ExcludeReplies = True
                                                                      params.MaximumNumberOfTweetsToRetrieve = 30
                                                                      Dim tweets = Timeline.GetUserTimeline(u, params)
                                                                      For Each tweet In tweets
                                                                          If Not tweet.Text.StartsWith("@") Then
                                                                              logger.Debug("Found previous tweet: " & tweet.Text)
                                                                              Me.PreviousTweets.Add(tweet.Text)
                                                                          End If
                                                                      Next
                                                                  End Sub)
            End Sub)
    End Sub

    Private Sub ReplyHandler(sender As Object, args As TweetReceivedEventArgs)
        Try
            InitTask.Wait()

            If args.Tweet.UserMentions.Any(Function(x) x.Id = MyId) And Not args.Tweet.IsRetweet Then
                If ReplyLimit <= 0 Then
                    Return
                End If
                ReplyLimit -= 1
                Task.Delay(60000).ContinueWith(Sub(t) ReplyLimit += 1)

                Dim split = args.Tweet.Text.Split(""""c)
                If split.Length = 3 AndAlso split(2).EndsWith("?") Then
                    Dim segment = split(1)
                    Dim movies = Me.FullTitles.Where(Function(s) s.IndexOf(segment, StringComparison.InvariantCultureIgnoreCase) >= 0).OrderBy(Function(s) R.Next())
                    Dim text = "@" & args.Tweet.CreatedBy.ScreenName & " Sorry, I don't know any movies that have that in the title."
                    If (movies.Any()) Then
                        text = "@" & args.Tweet.CreatedBy.ScreenName & " I found these movies: " & String.Join("; ", movies)
                        If text.Length > 140 Then
                            text = text.Substring(0, 139) + ChrW(8230)
                        End If
                    End If
                    logger.Info(Date.Now & ": " & text)
                    Auth.ExecuteOperationWithCredentials(Credentials, Sub() Tweet.PublishTweet(text))
                End If
            End If
        Catch ex As Exception
            logger.Error(ex)
        End Try
    End Sub

    Public Sub SendTweet()
        Try
            InitTask.Wait()

            logger.Trace(Date.Now)

            TweetTimer.Interval = Double.Parse(If(ConfigurationManager.AppSettings("IntervalMs"), "60000"))

            Dim i = 0
            Dim newTitle As String = Nothing

            Do While newTitle Is Nothing
                Dim index1 = R.Next(0, Titles.Count)
                Dim index2 = R.Next(0, Subtitles.Count)
                Dim part1 = Titles(index1)
                Dim part2 = Subtitles(index2)
                newTitle = part1 & part2

                If FullTitles.Contains(newTitle) Then
                    Continue Do
                End If

                If i >= 50 Then
                    Exit Do
                End If

                Dim similar = PreviousTweets.Where(Function(s) s.StartsWith(part1) Or s.EndsWith(part2))
                If similar.Any() Then
                    newTitle = Nothing
                End If

                i += 1
            Loop

            If newTitle IsNot Nothing Then
                logger.Info(Date.Now & ": " & newTitle)
                PreviousTweets.Add(newTitle)
                If PreviousTweets.Count > 30 Then
                    PreviousTweets.RemoveAt(0)
                End If
                Auth.ExecuteOperationWithCredentials(Credentials, Sub() Tweet.PublishTweet(newTitle))
            End If
        Catch ex As Exception
            logger.Error(ex)
        End Try
    End Sub

    Public Sub ServiceStart()
        Dim startTime = ConfigurationManager.AppSettings("StartTime")
        If startTime IsNot Nothing Then
            Dim startAt = Date.Today + TimeSpan.Parse(ConfigurationManager.AppSettings("StartTime"))
            If startAt < Date.Now Then
                startAt = startAt.AddDays(1)
            End If
            logger.Info("Will run at: " & startAt)
            TweetTimer.Interval = (startAt - Date.Now).TotalMilliseconds
        Else
            TweetTimer.Interval = Double.Parse(If(ConfigurationManager.AppSettings("IntervalMs"), "60000"))
        End If
        TweetTimer.Start()

        If UserStream IsNot Nothing Then
            UserStream.StartStream()
        End If
    End Sub

    Public Sub ServiceStop()
        TweetTimer.Stop()

        If UserStream IsNot Nothing Then
            UserStream.StopStream()
        End If
    End Sub
End Class
