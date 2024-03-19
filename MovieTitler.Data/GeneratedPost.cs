using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

#nullable disable

namespace MovieTitler.Data
{
    /// <summary>
    /// A generated post by the bot.
    /// </summary>
    public class GeneratedPost
    {
        /// <summary>
        /// An ID for the post.
        /// </summary>
        [Key, DatabaseGenerated(DatabaseGeneratedOption.None)]
        public int Id { get; set; }

        /// <summary>
        /// The post text (HTML).
        /// </summary>
        [Required]
        public string Content { get; set; }

        /// <summary>
        /// When the post text was generated.
        /// </summary>
        public DateTimeOffset CreatedAt { get; set; }
    }
}
