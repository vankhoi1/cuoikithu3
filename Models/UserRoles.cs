using System.ComponentModel.DataAnnotations;
using System.Data;

namespace QuanLyThuVien.Models
{
    public class UserRole
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [StringLength(50)]
        public required string Username { get; set; }

        [Required]
        [StringLength(50)]
        public required string RoleId { get; set; }

        public TaiKhoan? TaiKhoan { get; set; }
        public Role? Role { get; set; }
    }
}
