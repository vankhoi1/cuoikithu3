using System.Collections.Generic;

namespace QuanLyThuVien.Models
{
    public class HistoryViewModel
    {
        public List<BookLoan> LoanHistory { get; set; }
        public List<BookReservation> ReservationHistory { get; set; }
    }
}