using System.ComponentModel.DataAnnotations.Schema;

namespace DACS_Nhom3.Models
{
    public class UserFavoriteStory
    {
        public string UserId { get; set; }

        [ForeignKey("UserId")]
        public ApplicationUser User { get; set; }

        public int StoryId { get; set; }

        [ForeignKey("StoryId")]
        public Story Story { get; set; }

        public DateTime AddedAt { get; set; } = DateTime.Now;
    }
}