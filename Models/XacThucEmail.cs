using System;
using System.ComponentModel.DataAnnotations;

namespace QuanLyThuVien.Models
{
    public class XacThucEmail
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [StringLength(50)]
        public string Username { get; set; }

        [Required]
        [StringLength(10)]
        public string MaXacThuc { get; set; }

        public DateTime ThoiGianGui { get; set; }
    }
}
