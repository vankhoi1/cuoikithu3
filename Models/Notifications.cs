using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace QuanLyThuVien.Models
{
    public class Notification
    {
        [Key]
        public int NotificationId { get; set; }

        [Required(ErrorMessage = "Người dùng không được để trống")]
        public string Username { get; set; }

        [Required(ErrorMessage = "Nội dung thông báo không được để trống")]
        public string Message { get; set; }

        public DateTime CreatedAt { get; set; }

        public bool IsRead { get; set; }

        [ForeignKey("Username")]
        public TaiKhoan TaiKhoan { get; set; }
        public int? LoanId { get; set; }

    }
}