namespace MovieTitler.LowLevel

open System
open MovieTitler.Data

/// A user profile; in particular, the one created by this server.
type Person = {
    username: string
}

/// A post to expose over ActivityPub.
type Post = {
    id: int
    content: string
    created: DateTimeOffset
}

/// The main page of the user's post history (not the first page).
type PostHistory = {
    post_count: int
}

/// A single page of the user's post history.
type PostHistoryPage = {
    posts: Post list
    nextid: int
}

module Domain =
    let Actor: Person = {
        username = "bot"
    }

    let AsPost (generatedPost: GeneratedPost) = {
        id = generatedPost.Id
        content = generatedPost.Content
        created = generatedPost.CreatedAt
    }

    let AsGallery(count: int) = {
        post_count = count
    }

    let AsGalleryPage(posts: Post seq, nextid: int) = {
        posts = Seq.toList posts
        nextid = nextid
    }
