namespace QuanLyThuVien.Models
{
    // Lớp này dùng để hiển thị thông tin nhân viên kèm số lượt đã duyệt
    public class StaffWithStatsViewModel
    {
        public string Username { get; set; }
        public string Email { get; set; }
        public bool IsActive { get; set; }
        public int HandledActionsCount { get; set; } // Số hành động đã xử lý
    }
}