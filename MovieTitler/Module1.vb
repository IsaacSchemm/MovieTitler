Imports System.Configuration
Imports System.IO
Imports System.Timers
Imports Newtonsoft.Json
Imports Topshelf
Imports Tweetinvi
Imports Tweetinvi.Models

Module Module1

    Sub Main()
        '' Create a New set of credentials for the application.
        'Dim appCredentials = New TwitterCredentials(ConfigurationManager.AppSettings("ConsumerKey"),
        '                                     ConfigurationManager.AppSettings("ConsumerSecret"))

        '' Init the authentication process And store the related `AuthenticationContext`.
        'Dim authenticationContext = AuthFlow.InitAuthentication(appCredentials)

        '' Go to the URL so that Twitter authenticates the user And gives him a PIN code.
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
    Private Titles As IReadOnlyList(Of String)
    Private Subtitles As IReadOnlyList(Of String)
    Private Used As List(Of Tuple(Of Integer, Integer))

    Private Credentials As ITwitterCredentials
    Private TweetTimer As Timer
    Private Shared R As Random = New Random()

    Public Sub New()
        Dim sourceFileContents = File.ReadAllLines(ConfigurationManager.AppSettings("SourceFile"))
        Dim titles As New List(Of String)
        Dim subtitles As New List(Of String)
        For Each fullTitle In sourceFileContents
            Dim split1 = fullTitle.Split(vbTab)
            If split1.Length > 1 Then
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

        Console.WriteLine("Found " & titles.Count & " movies")

        Me.Titles = titles.Distinct().ToList()
        Console.WriteLine("Found " & Me.Titles.Count & " titles")
        Me.Subtitles = subtitles.Distinct().ToList()
        Console.WriteLine("Found " & Me.Subtitles.Count & " subtitles")
        Me.Used = New List(Of Tuple(Of Integer, Integer))

        TweetTimer = New Timer()
        AddHandler TweetTimer.Elapsed, AddressOf SendTweet
    End Sub

    Public Sub SendTweet()
        Console.WriteLine(Date.Now)

        TweetTimer.Interval = Double.Parse(If(ConfigurationManager.AppSettings("IntervalMs"), "60000"))

        If Credentials Is Nothing Then
            Dim jsonObj = JsonConvert.DeserializeObject(Of Dictionary(Of String, String))(File.ReadAllText(ConfigurationManager.AppSettings("KeysFile")))

            Credentials = New TwitterCredentials(jsonObj("ConsumerKey"),
                                                 jsonObj("ConsumerSecret"),
                                                 jsonObj("AccessToken"),
                                                 jsonObj("AccessTokenSecret"))
        End If

        Dim i = 0
        Dim newTitle As String = Nothing

        Do While newTitle Is Nothing
            Dim index1 = R.Next(0, Titles.Count)
            Dim index2 = R.Next(0, Subtitles.Count)
            Dim indices = New Tuple(Of Integer, Integer)(index1, index2)
            If Used.Contains(indices) And i < 10 Then
                i += 1
            Else
                Used.Add(indices)
                newTitle = Titles(index1) & Subtitles(index2)
            End If
        Loop

        Auth.ExecuteOperationWithCredentials(Credentials, Sub()
                                                              Tweet.PublishTweet(newTitle)
                                                              Console.WriteLine(newTitle)
                                                          End Sub)
    End Sub

    Public Sub ServiceStart()
        Dim startTime = ConfigurationManager.AppSettings("StartTime")
        If startTime IsNot Nothing Then
            Dim startAt = Date.Today + TimeSpan.Parse(ConfigurationManager.AppSettings("StartTime"))
            If startAt < Date.Now Then
                startAt = startAt.AddDays(1)
            End If
            TweetTimer.Interval = (startAt - Date.Now).TotalMilliseconds
        Else
            TweetTimer.Interval = Double.Parse(If(ConfigurationManager.AppSettings("IntervalMs"), "60000"))
        End If
        TweetTimer.Start()
    End Sub

    Public Sub ServiceStop()
        TweetTimer.Stop()
        Credentials = Nothing
    End Sub
End Class
