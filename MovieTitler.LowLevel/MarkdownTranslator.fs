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
            {enc title} - {enc appInfo.ApplicationName}
            </title>
            <meta name='viewport' content='width=device-width, initial-scale=1' />
            <style type='text/css'>
                body {{
                    text-align: center;
                    font-family: sans-serif;
                }}
            </style>
        </head>
        <body>
            {Markdig.Markdown.ToHtml(str)}
        </body>
        </html>
    """

    member _.ToMarkdown (post: Post) = String.concat "\n" [
        $"# {post.content}"
        $""
        $"""[{post.created.UtcDateTime.ToString("MMMM d, yyyy (hh:mm)")}]({mapper.GetObjectId(post.id)})"""
    ]

    member this.ToHtml (post: Post) =
        this.ToMarkdown post
        |> toHtml "Post"

    member this.ToMarkdown (person: Person, recentSubmissions: Post seq) = String.concat "\n" [
        for post in recentSubmissions do
            this.ToMarkdown(post)
            $""
            $"----------"
            $""
        $"`@{enc person.username}@{appInfo.ApplicationHostname}`"
        $""
        $"[View post history](/api/actor/outbox/page)"
        $""
        $"[Atom](/api/actor/outbox/page?format=atom)"
        $""
        $"[RSS](/api/actor/outbox/page?format=rss)"
        $""
        $"--------"
        $""
        $"<details>"
        $"<summary>About</summary>"
        $""
        $"[{enc appInfo.ApplicationName} {enc appInfo.VersionNumber}]({appInfo.WebsiteUrl})"
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
        $"</details>"
    ]

    member this.ToHtml (person: Person, recentSubmissions: Post seq) =
        this.ToMarkdown (person, recentSubmissions)
        |> toHtml person.username

    member this.ToMarkdown (page: OutboxPage) = String.concat "\n" [
        for post in page.posts do
            this.ToMarkdown(post)
            $""
            $"--------"
            $""
        if page.posts <> [] then
            $"[View more posts](/api/actor/outbox/page?nextid={List.min [for p in page.posts do p.id]})"
        else
            "No more posts are available."
        $""
    ]

    member this.ToHtml (page: OutboxPage) =
        this.ToMarkdown page
        |> toHtml "Post History"
