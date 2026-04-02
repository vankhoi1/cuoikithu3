using System.ComponentModel.DataAnnotations;

namespace QuanLyThuVien.Models
{
    public class SendOTPViewModel
    {
        [Required(ErrorMessage = "Vui lòng nhập tên người dùng")]
        [StringLength(50)]
        public string Username { get; set; }

        [Required(ErrorMessage = "Vui lòng nhập mật khẩu")]
        [StringLength(255)]
        [DataType(DataType.Password)]
        public string Password { get; set; }

        [Required(ErrorMessage = "Vui lòng nhập email")]
        [EmailAddress(ErrorMessage = "Email không hợp lệ")]
        [StringLength(100)]
        public string Email { get; set; }
        [Required(ErrorMessage = "Vui lòng nhập họ và tên")]
        public string HoTen { get; set; }

        [Phone(ErrorMessage = "Số điện thoại không hợp lệ")]
        public string? SoDienThoai { get; set; }
    }
}