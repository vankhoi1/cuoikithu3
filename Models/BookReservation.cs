using System;
using System.ComponentModel.DataAnnotations;

namespace QuanLyThuVien.Models
{
    public class BookReservation
    {
        [Key]
        public int ReservationId { get; set; }
        [Required(ErrorMessage = "Vui lòng chọn sách")]
        public int BookId { get; set; }
        [Required(ErrorMessage = "Vui lòng chọn người dùng")]
        public string Username { get; set; }
        [Required(ErrorMessage = "Vui lòng chọn ngày đặt trước")]
        public DateTime ReservationDate { get; set; }
        [Required]
        public string Status { get; set; } // Pending, Approved, Rejected
        public Book Book { get; set; }
        public TaiKhoan TaiKhoan { get; set; }
    }
}