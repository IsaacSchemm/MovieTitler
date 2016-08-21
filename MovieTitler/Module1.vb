Imports System.Configuration
Imports System.IO
Imports System.Timers
Imports Newtonsoft.Json
Imports Topshelf
Imports Tweetinvi
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
    Private FullTitles As IReadOnlyList(Of String)
    Private Titles As IReadOnlyList(Of String)
    Private Subtitles As IReadOnlyList(Of String)
    Private PreviousTweets As List(Of String)

    Private Credentials As ITwitterCredentials
    Private TweetTimer As Timer
    Private Shared R As Random = New Random()

    Private MyId As Long
    Private UserStream As IUserStream
    Private ReplyLimit As Integer

    Public Sub New()
        Dim sourceFileContents = File.ReadAllLines(ConfigurationManager.AppSettings("SourceFile"))
        Dim fullTitles As New List(Of String)
        Dim titles As New List(Of String)
        Dim subtitles As New List(Of String)
        For Each fullTitle In sourceFileContents
            Dim split1 = fullTitle.Split(vbTab)
            If split1.Length > 1 Then
                fullTitles.Add(split1(1))
                Dim index = Math.Max(split1(1).LastIndexOf(" - "), split1(1).LastIndexOf(": "))
                If index >= 0 Then
                    Dim title = split1(1).Substring(0, index)
                    Dim subtitle = split1(1).Substring(index)
                    If title IsNot "Mission" Then
                        titles.Add(title)
                        subtitles.Add(subtitle)
                    End If
                End If
            End If
        Next

        Me.FullTitles = fullTitles
        Console.WriteLine("Found " & fullTitles.Count & " movies")

        Me.Titles = titles.Distinct().ToList()
        Console.WriteLine("Found " & Me.Titles.Count & " titles")
        Me.Subtitles = subtitles.Distinct().ToList()
        Console.WriteLine("Found " & Me.Subtitles.Count & " subtitles")

        If Credentials Is Nothing Then
            Dim jsonObj = JsonConvert.DeserializeObject(Of Dictionary(Of String, String))(File.ReadAllText(ConfigurationManager.AppSettings("KeysFile")))

            Credentials = New TwitterCredentials(jsonObj("ConsumerKey"),
                                                 jsonObj("ConsumerSecret"),
                                                 jsonObj("AccessToken"),
                                                 jsonObj("AccessTokenSecret"))
        End If

        ReplyLimit = 6
        UserStream = Tweetinvi.Stream.CreateUserStream(Credentials)
        AddHandler UserStream.TweetCreatedByAnyoneButMe, Sub(sender, args)
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
                                                                     Auth.ExecuteOperationWithCredentials(Credentials, Sub() Tweet.PublishTweet(text))
                                                                 End If
                                                             End If
                                                         End Sub

        Me.PreviousTweets = New List(Of String)
        Auth.ExecuteOperationWithCredentials(Credentials, Sub()
                                                              Dim u = User.GetAuthenticatedUser()
                                                              Me.MyId = u.Id
                                                              Dim tweets = Timeline.GetUserTimeline(u, 30)
                                                              For Each tweet In tweets
                                                                  If Not tweet.Text.StartsWith("@") Then
                                                                      Console.WriteLine("Found previous tweet: " & tweet.Text)
                                                                      Me.PreviousTweets.Add(tweet.Text)
                                                                  End If
                                                              Next
                                                          End Sub)

        TweetTimer = New Timer()
        AddHandler TweetTimer.Elapsed, AddressOf SendTweet
    End Sub

    Public Sub SendTweet()
        Console.WriteLine(Date.Now)

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
            Console.WriteLine(newTitle)
            PreviousTweets.Add(newTitle)
            If PreviousTweets.Count > 30 Then
                PreviousTweets.RemoveAt(0)
            End If
            'Auth.ExecuteOperationWithCredentials(Credentials, Sub() Tweet.PublishTweet(newTitle))
        End If
    End Sub

    Public Sub ServiceStart()
        Dim startTime = ConfigurationManager.AppSettings("StartTime")
        If startTime IsNot Nothing Then
            Dim startAt = Date.Today + TimeSpan.Parse(ConfigurationManager.AppSettings("StartTime"))
            If startAt < Date.Now Then
                startAt = startAt.AddDays(1)
            End If
            Console.WriteLine("Will run at: " & startAt)
            TweetTimer.Interval = (startAt - Date.Now).TotalMilliseconds
        Else
            TweetTimer.Interval = Double.Parse(If(ConfigurationManager.AppSettings("IntervalMs"), "60000"))
        End If
        TweetTimer.Start()

        UserStream.StartStream()
    End Sub

    Public Sub ServiceStop()
        TweetTimer.Stop()
        UserStream.StopStream()
    End Sub
End Class
