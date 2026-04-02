using System.Collections.Generic;

namespace QuanLyThuVien.Models
{
    public class BookListViewModel
    {
        public List<Book> Books { get; set; }
        public HashSet<int> BorrowedBookIds { get; set; }
        public List<string> Genres { get; set; } // Thêm danh sách thể loại
        public HashSet<int> ReservedBookIds { get; set; }
        public HashSet<int> PendingLoanBookIds { get; set; } = new();
    }
}