using System.ComponentModel.DataAnnotations;

namespace QuanLyThuVien.Models
{
    public class LoginViewModel
    {
        [Required(ErrorMessage = "Vui lòng nhập tên người dùng hoặc email")]
        [StringLength(100, ErrorMessage = "Tên người dùng hoặc email không được vượt quá 100 ký tự")]
        public string UsernameOrEmail { get; set; }

        [Required(ErrorMessage = "Vui lòng nhập mật khẩu")]
        [StringLength(255)]
        [DataType(DataType.Password)]
        public string Password { get; set; }
    }
}