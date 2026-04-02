using System;
using System.ComponentModel.DataAnnotations;
using QuanLyThuVien.Validation;

namespace QuanLyThuVien.Models
{
    public class RegisterViewModel
    {
        [Required(ErrorMessage = "Vui lòng nhập tên người dùng")]
        public string Username { get; set; }

        [Required(ErrorMessage = "Vui lòng nhập mật khẩu")]
        [DataType(DataType.Password)]
        public string Password { get; set; }

        [Required(ErrorMessage = "Vui lòng nhập email")]
        [EmailAddress(ErrorMessage = "Địa chỉ email không hợp lệ")]
        public string Email { get; set; }

        [Required(ErrorMessage = "Vui lòng nhập họ và tên")]
        public string HoTen { get; set; }

        [Phone(ErrorMessage = "Số điện thoại không hợp lệ")]
        public string? SoDienThoai { get; set; }

        [DataType(DataType.Date)]
        [MinimumAge(10, ErrorMessage = "Bạn phải đủ 10 tuổi để đăng ký.")]
        public DateTime? NgaySinh { get; set; }

        public string? DiaChi { get; set; }

        public string? OTP { get; set; }
    }
}