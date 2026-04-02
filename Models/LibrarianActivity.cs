using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace QuanLyThuVien.Models
{
    public class LibrarianActivity
    {
        [Key]
        public int ActivityId { get; set; }

        [Required]
        public string Username { get; set; } // Username của thủ thư thực hiện

        [Required]
        public string Action { get; set; } // Hành động (ví dụ: "Duyệt mượn sách", "Xóa sách")

        public string? Details { get; set; } // Chi tiết về hành động (ví dụ: "Sách: Lão Hạc (ID: 15)")

        [Required]
        public DateTime Timestamp { get; set; } // Thời gian thực hiện

        [ForeignKey("Username")]
        public TaiKhoan TaiKhoan { get; set; }
    }
}