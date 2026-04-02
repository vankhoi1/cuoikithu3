using System.ComponentModel.DataAnnotations;

namespace QuanLyThuVien.Models
{
    public class TaiKhoan
    {
        [Key]
        [StringLength(50)]
        public string Username { get; set; }

        [Required]
        [StringLength(255)]
        public string Password { get; set; }

        [Required]
        [StringLength(100)]
        [EmailAddress]
        public string Email { get; set; }

        [Required]
        [StringLength(20)]
        public string LoaiTaiKhoan { get; set; }
        public bool IsActive { get; set; } = true;
    }
}