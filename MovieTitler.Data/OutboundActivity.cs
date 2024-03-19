using System.ComponentModel.DataAnnotations;

#nullable disable

namespace MovieTitler.Data
{
    /// <summary>
    /// An ActivityPub activity queued to be sent to an external inbox.
    /// </summary>
    public class OutboundActivity
    {
        /// <summary>
        /// An internal ID.
        /// </summary>
        public Guid Id { get; set; }

        /// <summary>
        /// The inbox ID / URL to send to.
        /// </summary>
        [Required]
        public string Inbox { get; set; }

        /// <summary>
        /// The pre-serialized JSON-LD body of the request.
        /// </summary>
        [Required]
        public string JsonBody { get; set; }

        /// <summary>
        /// When this activity was added to the bot's database. Activities
        /// will be removed once they are sent, or when they have been waiting
        /// for a long period of time without a succcessful send.
        /// </summary>
        public DateTimeOffset StoredAt { get; set; }

        /// <summary>
        /// If this date/time is in the future, this activity (and any further
        /// activities to the same inbox) should be delayed until at least the
        /// next run.
        /// </summary>
        public DateTimeOffset DelayUntil { get; set; }
    }
}
