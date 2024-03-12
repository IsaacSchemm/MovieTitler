using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

#nullable disable

namespace MovieTitler.Data
{
    /// <summary>
    /// A generated post by MovieTitler.
    /// </summary>
    public class GeneratedPost
    {
        /// <summary>
        /// An ID for the post.
        /// </summary>
        [Key, DatabaseGenerated(DatabaseGeneratedOption.None)]
        public int Id { get; set; }

        /// <summary>
        /// The generated movie title.
        /// </summary>
        [Required]
        public string MovieTitle { get; set; }

        /// <summary>
        /// When the movie title was generated.
        /// </summary>
        public DateTimeOffset CreatedAt { get; set; }
    }
}
