using System;

namespace QuanLyThuVien.Models
{
    public class ExtensionRequestViewModel
    {
        public int NotificationId { get; set; }
        public int LoanId { get; set; }
        public string BookTitle { get; set; }
        public string Username { get; set; }
        public DateTime RequestDate { get; set; }
        public DateTime? DueDate { get; set; }
        public DateTime? NewDueDate { get; set; }
    }
}