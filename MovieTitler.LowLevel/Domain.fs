namespace MovieTitler.LowLevel

open System
open MovieTitler.Data

/// A user profile; in particular, the one created by this server.
type Person = {
    username: string
    name: string
}

/// A post to expose over ActivityPub.
type Post = {
    id: int
    text: string
    created: DateTimeOffset
}

/// The user's outbox endpoint.
type Outbox = {
    post_count: int
}

/// A single page of the user's outbox.
type OutboxPage = {
    posts: Post list
    nextid: int
}

module Domain =
    let Actor: Person = {
        username = "bot"
        name = "MovieTitler"
    }

    let AsPost (generatedPost: GeneratedPost) = {
        id = generatedPost.Id
        text = generatedPost.TextContent
        created = generatedPost.CreatedAt
    }

    let AsOutbox(count: int) = {
        post_count = count
    }

    let AsOutboxPage(posts: Post seq, nextid: int) = {
        posts = Seq.toList posts
        nextid = nextid
    }
