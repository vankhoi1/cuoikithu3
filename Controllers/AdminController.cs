
namespace QuanLyThuVien.Controllers
{
    using Microsoft.AspNetCore.Authorization;
    using Microsoft.AspNetCore.Mvc;
    using Microsoft.EntityFrameworkCore;
    using QuanLyThuVien.Data;
    using QuanLyThuVien.Models; // <-- DÒNG QUAN TRỌNG NHẤT ĐỂ SỬA LỖI
    using System;
    using System.Linq;
    using System.Threading.Tasks;
    using ClosedXML.Excel;
    using System.IO;
    [Authorize(Roles = "Admin")]
    public class AdminController : Controller
    {
        private readonly LibraryDbContext _context;

        public AdminController(LibraryDbContext context)
        {
            _context = context;
        }

        // Trang Index giờ sẽ là dashboard chính của Admin
        public IActionResult Index()
        {
            return View();
        }
        // Sửa lại phương thức StaffActivityHistory trong AdminController.cs
        public async Task<IActionResult> StaffActivityHistory(string username)
        {
            if (string.IsNullOrEmpty(username))
            {
                return NotFound();
            }

            var staff = await _context.TaiKhoan.FirstOrDefaultAsync(t => t.Username == username && t.LoaiTaiKhoan == "ThuThu");
            if (staff == null)
            {
                TempData["ErrorMessage"] = "Không tìm thấy tài khoản nhân viên.";
                return RedirectToAction("ManageStaff");
            }

            var activities = await _context.LibrarianActivities
                .Where(a => a.Username == username)
                .OrderByDescending(a => a.Timestamp)
                .ToListAsync();

            // Tính toán số liệu và gửi qua ViewData
            ViewData["StaffUsername"] = username;
            ViewData["TotalHandledCount"] = activities.Count;

            // >>> THÊM LOGIC TÍNH TOÁN CHO HÔM NAY <<<
            var today = DateTime.Today;
            ViewData["TodayHandledCount"] = activities.Count(a => a.Timestamp.Date == today);

            return View(activities);
        }
        // --- Chức năng Quản lý tài khoản nhân viên (Đã có) ---
        // Thay thế phương thức ManageStaff cũ bằng phương thức mới này

        public async Task<IActionResult> ManageStaff()
        {
            // Lấy danh sách các tài khoản thủ thư
            var staffAccounts = await _context.TaiKhoan
                .Where(u => u.LoaiTaiKhoan == "ThuThu")
                .ToListAsync();

            // Lấy số lượng hành động đã xử lý (Duyệt, Từ chối, Trả sách...) cho mỗi thủ thư
            var handledActions = await _context.LibrarianActivities
                .Where(a => a.Action.Contains("Duyệt") ||
                             a.Action.Contains("Từ chối") ||
                             a.Action.Contains("Xác nhận trả sách"))
                .GroupBy(a => a.Username)
                .Select(g => new {
                    Username = g.Key,
                    Count = g.Count()
                })
                .ToListAsync();

            // Kết hợp thông tin tài khoản và thông tin thống kê
            var viewModel = staffAccounts.Select(staff => new StaffWithStatsViewModel
            {
                Username = staff.Username,
                Email = staff.Email,
                IsActive = staff.IsActive,
                HandledActionsCount = handledActions.FirstOrDefault(h => h.Username == staff.Username)?.Count ?? 0
            }).ToList();

            // --- Lấy số liệu các yêu cầu CHƯA DUYỆT (toàn hệ thống) ---
            ViewData["PendingLoanCount"] = await _context.BookLoans.CountAsync(l => l.Status == "Pending");
            ViewData["PendingExtensionCount"] = await _context.Notifications.CountAsync(n => n.Message.StartsWith("YEUCAUGIAHANSACHDOCGIA:") && !n.IsRead);

            return View(viewModel);
        }

        public IActionResult CreateStaff()
        {
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateStaff(CreateStaffViewModel model)
        {
            if (ModelState.IsValid)
            {
                if (await _context.TaiKhoan.AnyAsync(t => t.Username == model.Username))
                {
                    ModelState.AddModelError("Username", "Tên người dùng đã tồn tại.");
                    return View(model);
                }
                if (await _context.TaiKhoan.AnyAsync(t => t.Email == model.Email))
                {
                    ModelState.AddModelError("Email", "Email đã được sử dụng.");
                    return View(model);
                }

                var staffAccount = new TaiKhoan
                {
                    Username = model.Username,
                    Email = model.Email,
                    Password = BCrypt.Net.BCrypt.HashPassword(model.Password),
                    LoaiTaiKhoan = "ThuThu",
                    IsActive = true
                };

                _context.TaiKhoan.Add(staffAccount);
                await _context.SaveChangesAsync();
                TempData["SuccessMessage"] = "Đã tạo tài khoản nhân viên thành công.";
                return RedirectToAction("ManageStaff");
            }
            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ToggleStaffStatus(string username)
        {
            if (string.IsNullOrEmpty(username)) return NotFound();

            var user = await _context.TaiKhoan.FirstOrDefaultAsync(u => u.Username == username && u.LoaiTaiKhoan == "ThuThu");
            if (user != null)
            {
                user.IsActive = !user.IsActive;
                await _context.SaveChangesAsync();
                TempData["SuccessMessage"] = "Đã cập nhật trạng thái tài khoản nhân viên thành công!";
            }
            else
            {
                TempData["ErrorMessage"] = "Không tìm thấy tài khoản nhân viên.";
            }
            return RedirectToAction("ManageStaff");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteStaff(string username)
        {
            if (string.IsNullOrEmpty(username)) return NotFound();
            var user = await _context.TaiKhoan.FindAsync(username);
            if (user != null && user.LoaiTaiKhoan == "ThuThu")
            {
                _context.TaiKhoan.Remove(user);
                await _context.SaveChangesAsync();
                TempData["SuccessMessage"] = $"Đã xóa tài khoản nhân viên: {user.Username}";
            }
            else
            {
                TempData["ErrorMessage"] = "Không tìm thấy tài khoản nhân viên để xóa.";
            }
            return RedirectToAction("ManageStaff");
        }

        // --- CHỨC NĂNG MỚI: Thống kê trễ hạn ---
        public async Task<IActionResult> OverdueStatistics()
        {
            var overdueLoans = await _context.BookLoans
                .Include(l => l.Book)
                .Where(l => l.Status == "Approved" && l.ReturnDate == null && l.DueDate < DateTime.Now)
                .Select(l => new OverdueStatViewModel
                {
                    BookTitle = l.Book.Title,
                    Username = l.Username,
                    DueDate = l.DueDate,
                    DaysOverdue = l.DueDate.HasValue ? (int)(DateTime.Now - l.DueDate.Value).TotalDays : 0
                })
                .ToListAsync();
            return View(overdueLoans);
        }

        // --- CHỨC NĂNG MỚI: Thống kê sách mượn nhiều ---
        public async Task<IActionResult> PopularBooks()
        {
            var popularBooks = await _context.BookLoans
                // SỬA ĐỔI: Đếm tất cả các lượt mượn đã được duyệt (bao gồm cả đã trả)
                .Where(l => l.Status == "Approved" || l.Status == "Returned")
                .GroupBy(l => l.BookId)
                .Select(g => new
                {
                    BookId = g.Key,
                    LoanCount = g.Count()
                })
                .OrderByDescending(b => b.LoanCount)
                .Take(10)
                .Join(_context.Books,
                      popular => popular.BookId,
                      book => book.BookId,
                      (popular, book) => new PopularBookViewModel
                      {
                          Title = book.Title,
                          Author = book.Author,
                          LoanCount = popular.LoanCount
                      })
                .ToListAsync();
            return View(popularBooks);
        }
        [HttpPost]
        public async Task<IActionResult> ExportOverdueToExcel()
        {
            // 1. Lấy dữ liệu giống hệt như trang thống kê
            var overdueLoans = await _context.BookLoans
                .Include(l => l.Book)
                .Where(l => l.Status == "Approved" && l.ReturnDate == null && l.DueDate < DateTime.Now)
                .Select(l => new OverdueStatViewModel
                {
                    BookTitle = l.Book.Title,
                    Username = l.Username,
                    DueDate = l.DueDate,
                    DaysOverdue = (int)(DateTime.Now - l.DueDate.Value).TotalDays
                })
                .ToListAsync();

            // 2. Tạo file Excel với ClosedXML
            using (var workbook = new XLWorkbook())
            {
                var worksheet = workbook.Worksheets.Add("SachQuaHan");
                var currentRow = 1;

                // Header
                worksheet.Cell(currentRow, 1).Value = "Tên sách";
                worksheet.Cell(currentRow, 2).Value = "Người mượn";
                worksheet.Cell(currentRow, 3).Value = "Hạn trả";
                worksheet.Cell(currentRow, 4).Value = "Số ngày quá hạn";

                // Style cho Header
                worksheet.Row(1).Style.Font.Bold = true;

                // Body
                foreach (var item in overdueLoans)
                {
                    currentRow++;
                    worksheet.Cell(currentRow, 1).Value = item.BookTitle;
                    worksheet.Cell(currentRow, 2).Value = item.Username;
                    worksheet.Cell(currentRow, 3).Value = item.DueDate?.ToString("dd/MM/yyyy");
                    worksheet.Cell(currentRow, 4).Value = item.DaysOverdue;
                }

                // 3. Trả về file
                using (var stream = new MemoryStream())
                {
                    workbook.SaveAs(stream);
                    var content = stream.ToArray();
                    return File(
                        content,
                        "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                        "ThongKeSachQuaHan.xlsx");
                }
            }
        }

        [HttpPost]
        public async Task<IActionResult> ExportPopularToExcel()
        {
            // 1. Lấy dữ liệu
            var popularBooks = await _context.BookLoans
                .Where(l => l.Status == "Approved" || l.Status == "Returned")
                .GroupBy(l => l.BookId)
                .Select(g => new { BookId = g.Key, LoanCount = g.Count() })
                .OrderByDescending(b => b.LoanCount)
                .Take(20) // Lấy top 20 hoặc tùy chọn
                .Join(_context.Books,
                      popular => popular.BookId,
                      book => book.BookId,
                      (popular, book) => new PopularBookViewModel
                      {
                          Title = book.Title,
                          Author = book.Author,
                          LoanCount = popular.LoanCount
                      })
                .ToListAsync();

            // 2. Tạo file Excel
            using (var workbook = new XLWorkbook())
            {
                var worksheet = workbook.Worksheets.Add("SachPhoBien");
                var currentRow = 1;

                // Header
                worksheet.Cell(currentRow, 1).Value = "Tên sách";
                worksheet.Cell(currentRow, 2).Value = "Tác giả";
                worksheet.Cell(currentRow, 3).Value = "Số lượt mượn";
                worksheet.Row(1).Style.Font.Bold = true;

                // Body
                foreach (var book in popularBooks)
                {
                    currentRow++;
                    worksheet.Cell(currentRow, 1).Value = book.Title;
                    worksheet.Cell(currentRow, 2).Value = book.Author;
                    worksheet.Cell(currentRow, 3).Value = book.LoanCount;
                }

                // 3. Trả về file
                using (var stream = new MemoryStream())
                {
                    workbook.SaveAs(stream);
                    var content = stream.ToArray();
                    return File(
                        content,
                        "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                        "ThongKeSachPhoBien.xlsx");
                }
            }
        }
    }
}