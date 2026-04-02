using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace QuanLyThuVien.Models
{
    public class Book
    {
        [Key]
        public int BookId { get; set; }

        [Required]
        [StringLength(100)]
        public string Title { get; set; }

        [Required]
        [StringLength(50)]
        public string Author { get; set; }

        [StringLength(50)]
        public string Genre { get; set; }

        public int PublicationYear { get; set; }

        public bool IsAvailable => SoLuong > 0;

        [StringLength(500)]
        public string Description { get; set; }

        public ICollection<BookImage> BookImages { get; set; } = new List<BookImage>();

        public string? CoverImagePath { get; set; }

        public int SoLuong { get; set; }  // Số lượng sách còn trong thư viện

        public ICollection<BookReservation> BookReservations { get; set; } = new List<BookReservation>();
        public ICollection<BookLoan> BookLoans { get; set; } = new List<BookLoan>();
        public string? PdfPath { get; set; }

    }
}