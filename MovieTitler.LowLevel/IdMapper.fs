namespace MovieTitler.LowLevel

open System
open MovieTitler.Interfaces

/// Provides mappings between this bot's internal IDs and the public ActivityPub IDs of corresponding objects.
type IdMapper(appInfo: IApplicationInformation) =
    /// The ActivityPub actor ID of the bot hosted by this server.
    member _.ActorId =
        $"https://{appInfo.ApplicationHostname}/api/actor"

    /// Generates a random ActivityPub ID that is not intended to be looked up.
    member _.GenerateTransientId() =
        $"https://{appInfo.ApplicationHostname}#transient-{Guid.NewGuid()}"

    /// Determines the ActivityPub object ID for a post.
    member _.GetObjectId(id: int) =
        $"https://{appInfo.ApplicationHostname}/api/posts/{id}"

    /// Determines the ID to use for a Create activity for a post.
    member this.GetCreateId(id: int) =
        $"{this.GetObjectId(id)}#create"
