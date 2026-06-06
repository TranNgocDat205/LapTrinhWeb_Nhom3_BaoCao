using System.ComponentModel.DataAnnotations.Schema;

namespace DACS_Nhom3.Models
{
    public class UserFavoriteGenre
    {
        public string UserId { get; set; }

        [ForeignKey("UserId")]
        public ApplicationUser User { get; set; }

        public int GenreId { get; set; }

        [ForeignKey("GenreId")]
        public Genre Genre { get; set; }

        public DateTime AddedAt { get; set; } = DateTime.Now;
    }
}