using System.ComponentModel.DataAnnotations;
using System.Data;

namespace QuanLyThuVien.Models
{
    public class Role
    {
        [Key]
        [StringLength(50)]
        public required string RoleId { get; set; }

        [Required]
        [StringLength(50)]
        public required string RoleName { get; set; }
    }
}
