using System;

namespace QuanLyThuVien.Models
{
    public class OverdueStatViewModel
    {
        public string BookTitle { get; set; }
        public string Username { get; set; }
        public DateTime? DueDate { get; set; } // Sử dụng DateTime? thay vì DateTime
        public int DaysOverdue { get; set; }
    }
}