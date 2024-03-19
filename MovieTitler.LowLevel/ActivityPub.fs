namespace MovieTitler.LowLevel

open System
open System.Collections.Generic
open System.Text.Json
open MovieTitler.Interfaces

/// Contains functions for JSON-LD serialization.
module ActivityPubSerializer =
    /// A JSON-LD context that includes all fields used by MovieTitler.
    let Context: obj = [
        "https://w3id.org/security/v1"
        "https://www.w3.org/ns/activitystreams"
    ]

    /// Converts ActivityPub objects in string/object pair format to an
    /// acceptable JSON-LD rendition.
    let SerializeWithContext (apObject: IDictionary<string, obj>) = JsonSerializer.Serialize(dict [
        "@context", Context
        for p in apObject do
            p.Key, p.Value
    ])

/// Creates ActivityPub objects (in string/object pair format) for actors,
/// posts, and other objects tracked by MovieTitler.
type ActivityPubTranslator(mapper: IdMapper) =
    /// The MovieTitler actor ID.
    let actor = mapper.ActorId

    /// Creates a string/object pair (F# tuple) with the given key and value.
    let pair key value = (key, value :> obj)

    /// Checks whether the character is in the set that Weasyl allows for
    /// tags, which is a subset of what Mastodon allows.
    let isRestrictedSet c =
        Char.IsAscii(c)
        && (Char.IsLetterOrDigit(c) || c = '_')
        && not (Char.IsUpper(c))

    /// Builds a Person object for the MovieTitler actor.
    member _.PersonToObject (person: Person) (key: IActorKey) (appInfo: IApplicationInformation) = dict [
        pair "id" actor
        pair "type" "Person"
        pair "inbox" $"{actor}/inbox"
        pair "outbox" $"{actor}/outbox"
        pair "followers" $"{actor}/followers"
        pair "following" $"{actor}/following"
        pair "preferredUsername" person.username
        pair "name" person.username
        pair "url" actor
        pair "publicKey" {|
            id = $"{actor}#main-key"
            owner = actor
            publicKeyPem = key.Pem
        |}
    ]

    /// Builds a transient Update activity for the actor.
    member this.PersonToUpdate (person: Person) (key: IActorKey) = dict [
        pair "type" "Update"
        pair "id" (mapper.GenerateTransientId())
        pair "actor" actor
        pair "published" DateTimeOffset.UtcNow
        pair "object" (this.PersonToObject person key)
    ]

    /// Builds a Note object for a post.
    member _.AsObject (post: Post) = dict [
        let id = mapper.GetObjectId(post.id)

        pair "id" id
        pair "url" id

        pair "type" "Note"

        pair "attributedTo" actor
        pair "content" post.content
        pair "published" post.created
        pair "to" "https://www.w3.org/ns/activitystreams#Public"
        pair "cc" [$"{actor}/followers"]
    ]

    /// Builds a Create activity for a post.
    member this.ObjectToCreate (post: Post) = dict [
        pair "type" "Create"
        pair "id" $"{mapper.GetObjectId(post.id)}?view=create"
        pair "actor" actor
        pair "published" post.created
        pair "to" "https://www.w3.org/ns/activitystreams#Public"
        pair "cc" [$"{actor}/followers"]
        pair "object" (this.AsObject post)
    ]

    /// Builds a transient Update activity for a post.
    member this.ObjectToUpdate (post: Post) = dict [
        pair "type" "Update"
        pair "id" (mapper.GenerateTransientId())
        pair "actor" actor
        pair "published" DateTimeOffset.UtcNow
        pair "to" "https://www.w3.org/ns/activitystreams#Public"
        pair "cc" [$"{actor}/followers"]
        pair "object" (this.AsObject post)
    ]

    /// Builds a transient Delete activity for a post.
    member _.ObjectToDelete (post: Post) = dict [
        pair "type" "Delete"
        pair "id" (mapper.GenerateTransientId())
        pair "actor" actor
        pair "published" DateTimeOffset.UtcNow
        pair "to" "https://www.w3.org/ns/activitystreams#Public"
        pair "cc" [$"{actor}/followers"]
        pair "object" (mapper.GetObjectId(post.id))
    ]

    /// Builds a transient Accept activity to accept a follow request.
    member _.AcceptFollow (followId: string) = dict [
        pair "type" "Accept"
        pair "id" (mapper.GenerateTransientId())
        pair "actor" actor
        pair "object" followId
    ]

    /// Builds an OrderedCollection to represent the user's outbox.
    member _.AsOutbox (postHistory: PostHistory) = dict [
        pair "id" $"{actor}/outbox"
        pair "type" "OrderedCollection"
        pair "totalItems" postHistory.post_count
        pair "first" $"{actor}/outbox/page"
    ]

    /// Builds an OrderedCollectionPage to represent a single page of the user's outbox.
    member this.AsOutboxPage (id: string) (page: PostHistoryPage) = dict [
        pair "id" id
        pair "type" "OrderedCollectionPage"

        if page.posts <> [] then
            pair "next" $"{actor}/outbox/page?nextid={List.min [for p in page.posts do p.id]}"

        pair "partOf" $"{actor}/outbox"
        pair "orderedItems" [for p in page.posts do this.ObjectToCreate p]
    ]
