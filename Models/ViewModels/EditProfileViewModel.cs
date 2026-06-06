using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;

namespace DACS_Nhom3.Models.ViewModels
{
    public class EditProfileViewModel
    {
        [Required(ErrorMessage = "Vui lòng nhập họ tên")]
        [StringLength(100, ErrorMessage = "Họ tên không quá 100 ký tự")]
        [Display(Name = "Họ tên")]
        public string FullName { get; set; } = string.Empty;

        [Phone(ErrorMessage = "Số điện thoại không hợp lệ")]
        [Display(Name = "Số điện thoại")]
        public string? PhoneNumber { get; set; }

        [StringLength(250, ErrorMessage = "Địa chỉ không quá 250 ký tự")]
        [Display(Name = "Địa chỉ")]
        public string? Address { get; set; }

        [Range(1, 120, ErrorMessage = "Tuổi phải từ 1 đến 120")]
        [Display(Name = "Tuổi")]
        public int? Age { get; set; }

        [StringLength(20, ErrorMessage = "Giới tính không hợp lệ")]
        [Display(Name = "Giới tính")]
        public string? Gender { get; set; }

        public string? CurrentAvatar { get; set; }

        [Display(Name = "Avatar")]
        public IFormFile? AvatarFile { get; set; }

        [DataType(DataType.Password)]
        [Display(Name = "Mật khẩu hiện tại")]
        public string? CurrentPassword { get; set; }

        [DataType(DataType.Password)]
        [StringLength(100, MinimumLength = 6, ErrorMessage = "Mật khẩu mới phải có ít nhất 6 ký tự")]
        [Display(Name = "Mật khẩu mới")]
        public string? NewPassword { get; set; }

        [DataType(DataType.Password)]
        [Compare(nameof(NewPassword), ErrorMessage = "Xác nhận mật khẩu không khớp")]
        [Display(Name = "Xác nhận mật khẩu mới")]
        public string? ConfirmNewPassword { get; set; }

        public bool HasPassword { get; set; }
    }
}
