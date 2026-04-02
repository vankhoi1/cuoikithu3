using System.ComponentModel.DataAnnotations;

namespace QuanLyThuVien.Models
{
    public class CreateLoanViewModel
    {
        [Required(ErrorMessage = "Vui lòng nhập tên người dùng")]
        public string Username { get; set; }

        [Required(ErrorMessage = "Vui lòng nhập ID sách")]
        public int BookId { get; set; }
    }
}