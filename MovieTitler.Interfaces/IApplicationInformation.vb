''' <summary>
''' Provides the name and version number of the application.
''' </summary>
Public Interface IApplicationInformation
    ''' <summary>
    ''' The application name (e.g. "MovieTitler").
    ''' </summary>
    ''' <returns></returns>
    ReadOnly Property ApplicationName As String

    ''' <summary>
    ''' The application's version number.
    ''' </summary>
    ReadOnly Property VersionNumber As String

    ''' <summary>
    ''' The host / domain name used by the bot.
    ''' </summary>
    ReadOnly Property ApplicationHostname As String

    ''' <summary>
    ''' A URL to a website with more information about the application.
    ''' </summary>
    ''' <returns></returns>
    ReadOnly Property WebsiteUrl As String

    ''' <summary>
    ''' The user agent string for outgoing requests.
    ''' </summary>
    ''' <returns></returns>
    ReadOnly Property UserAgent As String
End Interface
