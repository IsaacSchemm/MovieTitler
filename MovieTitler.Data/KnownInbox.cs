using System.ComponentModel.DataAnnotations;

#nullable disable

namespace MovieTitler.Data
{
    /// <summary>
    /// The inbox of an actor or server that has sent messages to this bot in
    /// the past. Update and Delete activities are sent to these inboxes, in
    /// addition to followers.
    /// </summary>
    public class KnownInbox
    {
        /// <summary>
        /// An internal ID.
        /// </summary>
        public Guid Id { get; set; }

        /// <summary>
        /// The (shared or personal) inbox ID / URL.
        /// </summary>
        [Required]
        public string Inbox { get; set; }
    }
}
