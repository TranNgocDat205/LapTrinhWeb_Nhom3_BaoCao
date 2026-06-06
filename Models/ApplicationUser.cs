using Microsoft.AspNetCore.Identity;
using System.Xml.Linq;

namespace DACS_Nhom3.Models
{
    public class ApplicationUser : IdentityUser
    {
        public string FullName { get; set; }
        public string Avatar { get; set; }
        public string? Address { get; set; }
        public int? Age { get; set; }
        public string? Gender { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public DateTime? LastLoginAt { get; set; }
        public bool IsActive { get; set; } = true;


        // Điểm danh / kim cương
        public int Diamonds { get; set; } = 0;
        public int CheckInStreak { get; set; } = 0;
        public DateTime? LastCheckInDate { get; set; }

        // Vòng quay may mắn / tải miễn phí
        public DateTime? LastSpinDate { get; set; }
        public int FreeDownloads { get; set; } = 0;

        // Navigation properties
        public ICollection<UserFavoriteStory> FavoriteStories { get; set; }
        public ICollection<UserFavoriteGenre> FavoriteGenres { get; set; }
        public ICollection<Story> Stories { get; set; }
        public ICollection<Comment> Comments { get; set; }
        public ICollection<ReadingHistory> ReadingHistories { get; set; }
    }
}