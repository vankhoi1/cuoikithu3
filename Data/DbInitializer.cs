namespace QuanLyThuVien.Data
{
    public static class DbInitializer
    {
        public static void SeedAdminUser(LibraryDbContext context)
        {
            if (!context.TaiKhoan.Any(u => u.Username == "admin"))
            {
                var adminAccount = new QuanLyThuVien.Models.TaiKhoan
                {
                    Username = "admin",
                    Password = BCrypt.Net.BCrypt.HashPassword("Admin@123"),
                    Email = "admin@thuvien.com",
                    LoaiTaiKhoan = "Admin", // Đã đổi thành "Admin"
                    IsActive = true
                };
                context.TaiKhoan.Add(adminAccount);
                context.SaveChanges();
            }
        }
    }
}