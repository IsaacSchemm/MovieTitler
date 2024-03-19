namespace MovieTitler.LowLevel

open System.Net
open MovieTitler.Interfaces

/// Creates Markdown and HTML renditions of this bot's objects and pages, for
/// use in the HTML web interface, or (for debugging) by other non-ActivityPub
/// user agents.
type MarkdownTranslator(mapper: IdMapper, appInfo: IApplicationInformation) =
    /// Performs HTML encoding on a string. (HTML can be inserted into
    /// Markdown and will be included in the final HTML output.)
    let enc = WebUtility.HtmlEncode

    /// Renders an HTML page, given a title and a Markdown document.
    let toHtml (title: string) (str: string) = $"""
        <!DOCTYPE html>
        <html>
        <head>
            <title>
            $"{enc title} - ${enc appInfo.ApplicationName}"
            </title>
            <meta name='viewport' content='width=device-width, initial-scale=1' />
        </head>
        <body>
            {Markdig.Markdown.ToHtml(str)}
        </body>
        </html>
    """

    member _.ToMarkdown (person: Person, recentSubmissions: Post seq) = String.concat "\n" [
        for post in recentSubmissions do
            $"# {enc post.content}"
            $""
            $"----------"
            $""
        $"@{enc person.username}@{appInfo.ApplicationHostname}"
        $""
        $"[View post history](/api/actor/outbox/page)"
        $""
        $"[Atom](/api/actor/outbox/page?format=atom)"
        $""
        $"[RSS](/api/actor/outbox/page?format=rss)"
        $""
        $"--------"
        $""
        $"## [{enc appInfo.ApplicationName} {enc appInfo.VersionNumber}]({appInfo.WebsiteUrl})"
        $""
        $"This program is free software: you can redistribute it and/or modify"
        $"it under the terms of the GNU Affero General Public License as published"
        $"by the Free Software Foundation, either version 3 of the License, or"
        $"(at your option) any later version."
        $""
        $"This program is distributed in the hope that it will be useful,"
        $"but WITHOUT ANY WARRANTY; without even the implied warranty of"
        $"MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the"
        $"GNU Affero General Public License for more details."
    ]

    member this.ToHtml (person: Person, recentSubmissions: Post seq) =
        this.ToMarkdown (person, recentSubmissions)
        |> toHtml appInfo.ApplicationName

    member _.ToMarkdown (post: Post) = $"""
        # {enc post.content}

        [{post.created.UtcDateTime.ToString("MMM d, yyyy")}]({mapper.GetObjectId(post.id)})
    """

    member this.ToHtml (post: Post) =
        this.ToMarkdown post
        |> toHtml appInfo.ApplicationName

    member _.ToMarkdown (postHistory: PostHistory) = String.concat "\n" [
        $"## Post History"
        $""
        $"{postHistory.post_count} item(s)."
        $""
        $"[Start from first page](/api/actor/outbox/page)"
        $""
    ]

    member this.ToHtml (postHistory: PostHistory) =
        this.ToMarkdown postHistory
        |> toHtml $"{appInfo.ApplicationName} - Post History"

    member _.ToMarkdown (page: PostHistoryPage) = String.concat "\n" [
        $"## Posts"
        $""
        for post in page.posts do
            let post_url = mapper.GetObjectId(post.id)

            $"### [{enc post.content}]({post_url})"
            $""
            enc (post.created.UtcDateTime.ToString("MMM d, yyyy"))
            $""
        $""
        if page.posts <> [] then
            $"[View more posts](/api/actor/outbox/page?nextid={List.min [for p in page.posts do p.id]})"
        else
            "No more posts are available."
        $""
    ]

    member this.ToHtml (page: PostHistoryPage) =
        this.ToMarkdown page
        |> toHtml $"{appInfo.ApplicationName} - Post History"
