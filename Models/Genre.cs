using System.ComponentModel.DataAnnotations;

namespace DACS_Nhom3.Models
{
    public class Genre
    {
        [Key]
        public int Id { get; set; }

        [Required(ErrorMessage = "Tên thể loại không được để trống")]
        [StringLength(50, ErrorMessage = "Tên thể loại không quá 50 ký tự")]
        [Display(Name = "Tên thể loại")]
        public string Name { get; set; } = string.Empty;

        [StringLength(200)]
        [Display(Name = "Mô tả")]
        public string? Description { get; set; }

        [Display(Name = "Icon")]
        public string? Icon { get; set; }

        [Display(Name = "Màu sắc")]
        public string? Color { get; set; }

        [Display(Name = "Thứ tự")]
        public int SortOrder { get; set; } = 0;

        [Display(Name = "Trạng thái")]
        public bool IsActive { get; set; } = true;

        public ICollection<Story> Stories { get; set; } = new List<Story>();
        public ICollection<UserFavoriteGenre> UserFavorites { get; set; } = new List<UserFavoriteGenre>();
    }
}