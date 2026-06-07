using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Xml.Linq;

namespace DACS_Nhom3.Models
{
    public class Story
    {
        [Key]
        public int Id { get; set; }

        [Required(ErrorMessage = "Tên truyện không được để trống")]
        [StringLength(200, ErrorMessage = "Tên truyện không quá 200 ký tự")]
        [Display(Name = "Tên truyện")]
        public string Title { get; set; } = string.Empty;

        [StringLength(100, ErrorMessage = "Tên tác giả không quá 100 ký tự")]
        [Display(Name = "Tác giả")]
        public string Author { get; set; } = string.Empty;

        [Required(ErrorMessage = "Mô tả không được để trống")]
        [Display(Name = "Mô tả")]
        public string Description { get; set; } = string.Empty;

        [Display(Name = "Ảnh bìa")]
        public string CoverImageUrl { get; set; } = "/images/default-cover.jpg";

        [Required(ErrorMessage = "Nội dung không được để trống")]
        [Display(Name = "Nội dung")]
        public string Content { get; set; } = string.Empty;

        [Display(Name = "Lượt xem")]
        public int Views { get; set; } = 0;

        [Display(Name = "Lượt thích")]
        public int Likes { get; set; } = 0;

        [Display(Name = "Trạng thái")]
        public StoryStatus Status { get; set; } = StoryStatus.Published;

        [Display(Name = "Ngày tạo")]
        public DateTime CreatedAt { get; set; } = DateTime.Now;

        [Display(Name = "Ngày cập nhật")]
        public DateTime UpdatedAt { get; set; } = DateTime.Now;

        [Display(Name = "Người đăng")]
        public string UserId { get; set; } = string.Empty;

        [ForeignKey("UserId")]
        public ApplicationUser? User { get; set; }

        [Display(Name = "Thể loại")]
        public int GenreId { get; set; }

        [ForeignKey("GenreId")]
        public Genre? Genre { get; set; }

        // Navigation properties
        public ICollection<UserFavoriteStory> UserFavorites { get; set; } = new List<UserFavoriteStory>();
        public ICollection<Comment> Comments { get; set; } = new List<Comment>();
        public ICollection<ReadingHistory> ReadingHistories { get; set; } = new List<ReadingHistory>();
    }

    public enum StoryStatus
    {
        [Display(Name = "Nháp")]
        Draft,

        [Display(Name = "Đã đăng")]
        Published,

        [Display(Name = "Đã khóa")]
        Locked
    }
}