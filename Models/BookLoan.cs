using System;
using System.ComponentModel.DataAnnotations;

namespace QuanLyThuVien.Models
{
    public class BookLoan
    {
        [Key]
        public int LoanId { get; set; }
        [Required(ErrorMessage = "Vui lòng chọn sách")]
        public int BookId { get; set; }
        [Required(ErrorMessage = "Vui lòng chọn người dùng")]
        public string Username { get; set; }
        [Required(ErrorMessage = "Vui lòng chọn ngày mượn")]
        public DateTime BorrowDate { get; set; } // Non-nullable
        public DateTime? DueDate { get; set; } // Nullable
        public DateTime? ReturnDate { get; set; } // Nullable
        public bool IsOverdue { get; set; }
        [Required]
        public string Status { get; set; } // Pending, Approved, Rejected
        public Book Book { get; set; }
        public TaiKhoan TaiKhoan { get; set; }
        public bool WasExtended { get; set; } = false;
    }
}