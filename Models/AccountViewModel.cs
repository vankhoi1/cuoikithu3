namespace QuanLyThuVien.Models
{
    public class AccountViewModel
    {
        public required string Username { get; set; }
        public required string Email { get; set; }
        public List<string> Roles { get; set; } = new();
        public bool IsActive { get; set; }
    }
}