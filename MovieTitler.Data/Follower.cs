using System.ComponentModel.DataAnnotations;

#nullable disable

namespace MovieTitler.Data
{
    /// <summary>
    /// An ActivityPub user who is following MovieTitler.
    /// </summary>
    public class Follower
    {
        /// <summary>
        /// An internal ID for this follower.
        /// </summary>
        public Guid Id { get; set; }

        /// <summary>
        /// The follower's actor ID.
        /// </summary>
        public string ActorId { get; set; }

        /// <summary>
        /// The ID of the Follow activity that this actor sent.
        /// </summary>
        [Required]
        public string MostRecentFollowId { get; set; }

        /// <summary>
        /// This actor's personal ActivityPub inbox.
        /// </summary>
        [Required]
        public string Inbox { get; set; }

        /// <summary>
        /// The shared inbox of this actor's ActivityPub server, if any.
        /// </summary>
        public string SharedInbox { get; set; }
    }
}
