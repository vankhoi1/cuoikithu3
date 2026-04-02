using System.Collections.Generic;

namespace QuanLyThuVien.Models
{
    public class LoanDashboardViewModel
    {
        // Chứa danh sách các yêu cầu mượn sách đang chờ duyệt
        public List<BookLoan> PendingLoans { get; set; }

        // Chứa danh sách các sách đã được duyệt và đang mượn
        public List<BookLoan> ActiveLoans { get; set; }

        public List<ExtensionRequestViewModel> ExtensionRequests { get; set; }
    }
}