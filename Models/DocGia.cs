using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace QuanLyThuVien.Models
{
    public class DocGia
    {
        [Key]
        public int DocGiaId { get; set; }

        [Required]
        public string HoTen { get; set; }

        public DateTime? NgaySinh { get; set; }

        public string? DiaChi { get; set; }

        public string? SoDienThoai { get; set; }

        // Khóa ngoại liên kết với bảng TaiKhoan
        [Required]
        public string Username { get; set; }

        [ForeignKey("Username")]
        public TaiKhoan TaiKhoan { get; set; }
    }
}