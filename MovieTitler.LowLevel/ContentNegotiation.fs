namespace MovieTitler.LowLevel

open System.Linq
open System.Net.Http.Headers
open Microsoft.Net.Http.Headers
open MovieTitler.Interfaces

/// A type of formatting that MovieTitler supports for HTTP responses.
type OutputFormatFamily = Markdown | HTML | ActivityPub | RSS | Atom

/// An output format that MovieTitler supports, consisting of the general type of response (family) and an HTTP Content-Type value.
type OutputFormat = {
    Family: OutputFormatFamily
    MediaType: string
}

/// An object that helps MovieTitler determine the appropriate response type for an HTTP request.
type ContentNegotiator(appInfo: IApplicationInformation) =
    /// Builds an OutputFormat object.
    let format family mediaType = {
        Family = family
        MediaType = mediaType
    }

    /// A list of all response types supported by MovieTitler, in the order that MovieTitler prefers to use them.
    let supported = [
        // Markdown / plain text responses (may be useful for debugging).
        format Markdown "text/plain"
        format Markdown "text/markdown"

        // ActivityPub responses, for intercommunication with other ActivityPub software like Mastodon.
        format ActivityPub "application/activity+json"
        format ActivityPub "application/ld+json; profile=\"https://www.w3.org/ns/activitystreams\""
        format ActivityPub "application/json"
        format ActivityPub "text/json"

        // HTML responses for web browsers.
        format HTML "text/html"
    ]

    let parse (str: string) =
        MediaTypeHeaderValue.Parse(str)

    let sortByQuality (values: MediaTypeHeaderValue seq) =
        values.OrderByDescending(id, MediaTypeHeaderValueComparer.QualityComparer)

    /// The RSS 2.0 feed format.
    member _.RSS = format RSS "application/rss+xml"

    // The Atom feed format.
    member _.Atom = format Atom "application/atom+xml"

    /// Given an HTTP request, parses the Accept header to determine which
    /// response type(s) can be used, in order from most to least preferred
    /// (with possible duplicates).
    member _.GetAcceptableFormats(headers: HttpHeaders) = seq {
        let parsed =
            headers.GetValues("Accept")
            |> Seq.map parse
            |> sortByQuality
        for acceptedType in parsed do
            for candidate in supported do
                let responseType = parse candidate.MediaType
                if responseType.IsSubsetOf(acceptedType) then
                    yield candidate
    }
