﻿namespace MovieTitler.HighLevel.Remote
{
    /// <summary>
    /// Represents an actor on a remote ActivityPub server.
    /// </summary>
    /// <param name="Id">The actor ID / URL.</param>
    /// <param name="Inbox">The personal ActivityPub inbox for the user.</param>
    /// <param name="SharedInbox">The shared ActivityPub inbox for the user's server, if any.</param>
    /// <param name="KeyId">The ID of the user's signing key.</param>
    /// <param name="KeyPem">The user's public key in PEM format.</param>
    public record RemoteActor(
        string Id,
        string Inbox,
        string? SharedInbox,
        string KeyId,
        string KeyPem);
}
