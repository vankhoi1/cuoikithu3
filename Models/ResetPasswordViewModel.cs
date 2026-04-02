using System.ComponentModel.DataAnnotations;

namespace QuanLyThuVien.Models
{
    public class ResetPasswordViewModel
    {
        [Required(ErrorMessage = "Vui lòng nhập tên người dùng")]
        [StringLength(50)]
        public string Username { get; set; }

        [Required(ErrorMessage = "Vui lòng nhập mã OTP")]
        [StringLength(10)]
        public string OTP { get; set; }

        [Required(ErrorMessage = "Vui lòng nhập mật khẩu mới")]
        [StringLength(255)]
        [DataType(DataType.Password)]
        public string NewPassword { get; set; }
    }
}