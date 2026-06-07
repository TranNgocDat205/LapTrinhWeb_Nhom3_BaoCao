using System.ComponentModel.DataAnnotations.Schema;

namespace DACS_Nhom3.Models
{
    public class ReadingHistory
    {
        public int Id { get; set; }

        public string UserId { get; set; }

        [ForeignKey("UserId")]
        public ApplicationUser User { get; set; }

        public int StoryId { get; set; }

        [ForeignKey("StoryId")]
        public Story Story { get; set; }

        public DateTime ReadAt { get; set; } = DateTime.Now;

        public int CurrentPosition { get; set; } // Vị trí đọc hiện tại (ký tự)

        public int ReadCount { get; set; } = 1;
    }
}