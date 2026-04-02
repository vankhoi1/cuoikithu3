using System.Collections.Generic;

namespace QuanLyThuVien.Models
{
    public class ReaderStatusViewModel
    {
        // Danh sách các sách đang mượn
        public List<BookLoan> CurrentlyBorrowed { get; set; }

        // Danh sách các yêu cầu đặt trước đang chờ
        public List<BookReservation> PendingReservations { get; set; }
    }
}