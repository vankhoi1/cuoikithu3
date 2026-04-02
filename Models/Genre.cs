using System.ComponentModel.DataAnnotations;

namespace QuanLyThuVien.Models
{
    public class Genre
    {
        [Key]
        public int GenreId { get; set; }

        [Required]
        [StringLength(50)]
        public string Name { get; set; }
    }
}