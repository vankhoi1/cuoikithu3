using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using QuanLyThuVien.Data;
using QuanLyThuVien.Models;
using QuanLyThuVien.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;

namespace QuanLyThuVien.Controllers
{
    public class AccountController : Controller
    {
        private readonly LibraryDbContext _context;
        private readonly GmailEmailService _emailService;
        private readonly ILogger<AccountController> _logger;

        public AccountController(LibraryDbContext context, GmailEmailService emailService, ILogger<AccountController> logger)
        {
            _context = context;
            _emailService = emailService;
            _logger = logger;
        }

        [AllowAnonymous]
        public IActionResult Register()
        {
            return View();
        }

        [AllowAnonymous]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SendOTP(RegisterViewModel model)
        {
            ModelState.Remove("OTP");

            if (!ModelState.IsValid)
            {
                var errorMsg = string.Join("; ", ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage));
                return BadRequest("Dữ liệu không hợp lệ: " + errorMsg);
            }

            if (model.NgaySinh.HasValue)
            {
                var today = DateTime.Today;
                var age = today.Year - model.NgaySinh.Value.Year;
                if (model.NgaySinh.Value.Date > today.AddYears(-age)) age--;

                if (age < 10)
                {
                    return BadRequest("Bạn phải đủ 10 tuổi để đăng ký.");
                }
            }

            if (await _context.TaiKhoan.AnyAsync(t => t.Email == model.Email))
                return BadRequest("Email đã được sử dụng.");
            if (await _context.TaiKhoan.AnyAsync(t => t.Username == model.Username))
                return BadRequest("Tên người dùng đã tồn tại.");

            var otp = new Random().Next(100000, 999999).ToString();

            var taiKhoan = new TaiKhoan
            {
                Username = model.Username,
                Password = BCrypt.Net.BCrypt.HashPassword(model.Password),
                Email = model.Email,
                LoaiTaiKhoan = "DocGia"
            };
            await _context.TaiKhoan.AddAsync(taiKhoan);

            var docGia = new DocGia
            {
                Username = model.Username,
                HoTen = model.HoTen,
                SoDienThoai = model.SoDienThoai,
                NgaySinh = model.NgaySinh,
                DiaChi = model.DiaChi
            };
            await _context.DocGia.AddAsync(docGia);

            var xacThuc = new XacThucEmail
            {
                Username = model.Username,
                MaXacThuc = otp,
                ThoiGianGui = DateTime.Now
            };
            await _context.XacThucEmail.AddAsync(xacThuc);

            await _context.SaveChangesAsync();

            bool emailSent = await _emailService.SendEmailAsync(model.Email, "Mã OTP đăng ký", $"Mã OTP của bạn là: {otp}");
            if (!emailSent)
            {
                _context.TaiKhoan.Remove(taiKhoan);
                _context.DocGia.Remove(docGia);
                _context.XacThucEmail.Remove(xacThuc);
                await _context.SaveChangesAsync();
                return StatusCode(500, "Không thể gửi mã OTP. Vui lòng thử lại sau.");
            }

            return Ok("OTP đã được gửi thành công.");
        }

        [AllowAnonymous]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Register(RegisterViewModel model)
        {
            if (string.IsNullOrEmpty(model.OTP))
            {
                ModelState.AddModelError("OTP", "Vui lòng nhập mã OTP");
                return View(model);
            }

            var xacThuc = await _context.XacThucEmail.FirstOrDefaultAsync(x => x.Username == model.Username && x.MaXacThuc == model.OTP);
            if (xacThuc == null || xacThuc.ThoiGianGui < DateTime.Now.AddMinutes(-10))
            {
                ModelState.AddModelError("OTP", "Mã OTP không hợp lệ hoặc đã hết hạn.");
                return View(model);
            }

            _context.XacThucEmail.Remove(xacThuc);
            await _context.SaveChangesAsync();

            return RedirectToAction("Login");
        }

        [AllowAnonymous]
        public IActionResult Login() => View();

        [AllowAnonymous]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Login(LoginViewModel model)
        {
            if (!ModelState.IsValid) return View(model);

            var taiKhoan = await _context.TaiKhoan.FirstOrDefaultAsync(t => t.Username == model.UsernameOrEmail || t.Email == model.UsernameOrEmail);

            if (taiKhoan == null || !BCrypt.Net.BCrypt.Verify(model.Password, taiKhoan.Password))
            {
                ModelState.AddModelError(string.Empty, "Thông tin đăng nhập không chính xác.");
                return View(model);
            }
            if (!taiKhoan.IsActive)
            {
                ModelState.AddModelError(string.Empty, "Tài khoản này đã bị vô hiệu hóa.");
                return View(model);
            }
            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.Name, taiKhoan.Username),
                new Claim(ClaimTypes.Role, taiKhoan.LoaiTaiKhoan),
                new Claim(ClaimTypes.Email, taiKhoan.Email)
            };
            var claimsIdentity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
            await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, new ClaimsPrincipal(claimsIdentity));
            if (taiKhoan.LoaiTaiKhoan == "Admin")
            {
                return RedirectToAction("Index", "Admin");
            }
            if (taiKhoan.LoaiTaiKhoan == "ThuThu")
            {
                return RedirectToAction("Index", "Librarian");
            }
            return RedirectToAction("Index", "Home");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Logout()
        {
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            _logger.LogInformation("Đăng xuất thành công.");
            return RedirectToAction("Login");
        }

        [AllowAnonymous]
        public IActionResult ForgotPassword() => View();

        [AllowAnonymous]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ForgotPassword(ForgotPasswordViewModel model)
        {
            if (!ModelState.IsValid) return View(model);

            var taiKhoan = await _context.TaiKhoan.FirstOrDefaultAsync(t => t.Email == model.Email);
            if (taiKhoan == null)
            {
                ModelState.AddModelError(string.Empty, "Email không tồn tại.");
                return View(model);
            }

            var otp = new Random().Next(100000, 999999).ToString();
            var xacThuc = new XacThucEmail
            {
                Username = taiKhoan.Username,
                MaXacThuc = otp,
                ThoiGianGui = DateTime.Now
            };
            await _context.XacThucEmail.AddAsync(xacThuc);
            await _context.SaveChangesAsync();

            bool emailSent = await _emailService.SendEmailAsync(model.Email, "Mã OTP đặt lại mật khẩu", $"Mã OTP của bạn là: {otp}");
            if (!emailSent)
            {
                _context.XacThucEmail.Remove(xacThuc);
                await _context.SaveChangesAsync();
                return StatusCode(500, "Không thể gửi mã OTP. Vui lòng thử lại sau.");
            }

            return RedirectToAction("ResetPassword", new { username = taiKhoan.Username });
        }

        [AllowAnonymous]
        public IActionResult ResetPassword(string username) => View(new ResetPasswordViewModel { Username = username });

        [AllowAnonymous]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ResetPassword(ResetPasswordViewModel model)
        {
            if (!ModelState.IsValid) return View(model);

            var xacThuc = await _context.XacThucEmail.FirstOrDefaultAsync(x => x.Username == model.Username && x.MaXacThuc == model.OTP);
            if (xacThuc == null || xacThuc.ThoiGianGui < DateTime.Now.AddMinutes(-10))
            {
                ModelState.AddModelError("OTP", "Mã OTP không hợp lệ hoặc đã hết hạn.");
                return View(model);
            }

            var taiKhoan = await _context.TaiKhoan.FirstOrDefaultAsync(t => t.Username == model.Username);
            if (taiKhoan == null)
            {
                ModelState.AddModelError(string.Empty, "Tài khoản không tồn tại.");
                return View(model);
            }

            taiKhoan.Password = BCrypt.Net.BCrypt.HashPassword(model.NewPassword);
            _context.XacThucEmail.Remove(xacThuc);
            await _context.SaveChangesAsync();

            return RedirectToAction("Login");
        }
        [Authorize(Roles = "DocGia")]
        public async Task<IActionResult> BorrowingHistory()
        {
            var username = User.Identity.Name;

            // Lấy tất cả lịch sử mượn sách
            var loanHistory = await _context.BookLoans
                .Include(l => l.Book)
                .Where(l => l.Username == username)
                .OrderByDescending(l => l.BorrowDate)
                .ToListAsync();

            // Lấy tất cả lịch sử đặt trước
            var reservationHistory = await _context.BookReservations
                .Include(r => r.Book)
                .Where(r => r.Username == username)
                .OrderByDescending(r => r.ReservationDate)
                .ToListAsync();

            var viewModel = new HistoryViewModel
            {
                LoanHistory = loanHistory,
                ReservationHistory = reservationHistory
            };

            return View(viewModel);
        }

        [Authorize(Roles = "DocGia")]
        public async Task<IActionResult> ExtendLoan(int loanId)
        {
            var username = User.Identity.Name;

            // Sử dụng transaction để đảm bảo tất cả các thao tác hoặc thành công hoặc thất bại cùng nhau
            using (var transaction = await _context.Database.BeginTransactionAsync())
            {
                try
                {
                    // Bước 1: Lấy phiếu mượn và KHÓA nó lại để các request khác không thể sửa đổi
                    var loan = await _context.BookLoans
                        .Include(l => l.Book)
                        .FirstOrDefaultAsync(l => l.LoanId == loanId && l.Username == username && l.Status == "Approved");

                    if (loan == null)
                    {
                        TempData["ErrorMessage"] = "Không tìm thấy phiếu mượn.";
                        return RedirectToAction("LoanStatus");
                    }

                    // Bước 2: KIỂM TRA ĐIỀU KIỆN NGAY LẬP TỨC
                    if (loan.WasExtended)
                    {
                        // Nếu cờ này đã được bật (do một request khác nhanh hơn), lập tức từ chối.
                        TempData["ErrorMessage"] = "Yêu cầu gia hạn của bạn đang được xử lý hoặc đã được gửi đi.";
                        return RedirectToAction("LoanStatus");
                    }
                    if (loan.ReturnDate != null)
                    {
                        TempData["ErrorMessage"] = "Sách đã được trả, không thể gia hạn.";
                        return RedirectToAction("LoanStatus");
                    }

                    if (await _context.BookReservations.AnyAsync(r => r.BookId == loan.BookId && r.Status == "Pending"))
                    {
                        TempData["ErrorMessage"] = "Sách này đã có người khác đặt trước, không thể gia hạn.";
                        return RedirectToAction("LoanStatus");
                    }


                    // Bước 3: ĐÁNH DẤU VÀ LƯU NGAY LẬP TỨC ĐỂ "KHÓA"
                    // Đây là bước quan trọng nhất để chống Race Condition
                    loan.WasExtended = true;
                    await _context.SaveChangesAsync();

                    // Bước 4: Bây giờ mới tạo các thông báo một cách an toàn
                    var librarianMessage = $"YEUCAUGIAHANSACHDOCGIA: Độc giả '{username}' yêu cầu gia hạn cho sách '{loan.Book.Title}' (Phiếu mượn #{loanId}).";
                    var extensionRequestNotification = new Notification
                    {
                        Username = (await _context.TaiKhoan.FirstOrDefaultAsync(t => t.LoaiTaiKhoan == "ThuThu"))?.Username,
                        Message = librarianMessage,
                        CreatedAt = DateTime.Now,
                        IsRead = false,
                        LoanId = loan.LoanId
                    };
                    await _context.Notifications.AddAsync(extensionRequestNotification);

                    var userMessage = $"Bạn đã gửi yêu cầu gia hạn cho sách '{loan.Book.Title}'. Vui lòng chờ thủ thư duyệt.";
                    var userNotification = new Notification
                    {
                        Username = username,
                        Message = userMessage,
                        CreatedAt = DateTime.Now,
                        IsRead = false,
                        LoanId = loan.LoanId
                    };
                    await _context.Notifications.AddAsync(userNotification);

                    // Bước 5: Lưu lại các thông báo và commit transaction
                    await _context.SaveChangesAsync();
                    await transaction.CommitAsync();

                    TempData["SuccessMessage"] = "Đã gửi yêu cầu gia hạn thành công. Vui lòng chờ thủ thư duyệt.";
                    return RedirectToAction("LoanStatus");
                }
                catch (Exception ex)
                {
                    await transaction.RollbackAsync();
                    _logger.LogError(ex, "Lỗi nghiêm trọng khi xử lý gia hạn cho LoanId: {LoanId}", loanId);
                    TempData["ErrorMessage"] = "Có lỗi xảy ra, vui lòng thử lại.";
                    return RedirectToAction("LoanStatus");
                }
            }
        }
        [Authorize(Roles = "DocGia")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ReserveBook(int bookId)
        {
            try
            {
                var username = User.Identity.Name;

                // --- KIỂM TRA CÁC ĐIỀU KIỆN ---
                var hasOverdueBooks = await _context.BookLoans
                    .AnyAsync(l => l.Username == username && l.Status == "Approved" && l.ReturnDate == null && l.DueDate < DateTime.Now);

                if (hasOverdueBooks)
                {
                    TempData["ErrorMessage"] = "Bạn có sách đang mượn quá hạn. Vui lòng trả sách trước khi đặt trước sách mới.";
                    return RedirectToAction("BookDetails", new { bookId = bookId });
                }

                var book = await _context.Books.FindAsync(bookId);
                if (book == null)
                {
                    TempData["ErrorMessage"] = "Sách không tồn tại.";
                    return RedirectToAction("BookDetails", new { bookId });
                }

                // THÊM LOGIC MỚI: KIỂM TRA NẾU ĐỘC GIẢ ĐANG MƯỢN SÁCH NÀY
                // Kiểm tra xem độc giả có đang có một bản mượn đang được duyệt và chưa trả cho cuốn sách này không.
                var hasActiveLoan = await _context.BookLoans
                    .AnyAsync(l => l.BookId == bookId && l.Username == username && (l.Status == "Approved" && l.ReturnDate == null));
                if (hasActiveLoan)
                {
                    TempData["ErrorMessage"] = "Bạn đang mượn sách này, không thể đặt trước.";
                    return RedirectToAction("BookDetails", new { bookId });
                }
                // KẾT THÚC LOGIC THÊM

                if (book.IsAvailable)
                {
                    TempData["ErrorMessage"] = "Sách này hiện đang có sẵn, không cần đặt trước.";
                    return RedirectToAction("BookDetails", new { bookId });
                }

                if (await _context.BookReservations.AnyAsync(r => r.BookId == bookId && r.Username == username && r.Status == "Pending"))
                {
                    TempData["ErrorMessage"] = "Bạn đã đặt trước sách này rồi.";
                    return RedirectToAction("BookDetails", new { bookId });
                }

                var reservation = new BookReservation
                {
                    BookId = bookId,
                    Username = username,
                    ReservationDate = DateTime.Now,
                    Status = "Pending"
                };
                await _context.BookReservations.AddAsync(reservation);

                // Gửi thông báo cho thủ thư
                var librarianMessage = $"Độc giả '{username}' đã đặt trước sách '{book.Title}'.";
                var librarians = await _context.TaiKhoan.Where(t => t.LoaiTaiKhoan == "ThuThu").ToListAsync();
                foreach (var librarian in librarians)
                {
                    var librarianNotification = new Notification
                    {
                        Username = librarian.Username,
                        Message = librarianMessage,
                        CreatedAt = DateTime.Now,
                        IsRead = false
                    };
                    await _context.Notifications.AddAsync(librarianNotification);
                }

                // Gửi thông báo cho độc giả
                var userMessage = $"Bạn đã gửi yêu cầu đặt trước sách '{book.Title}'. Vui lòng chờ thủ thư duyệt.";
                var userAccount = await _context.TaiKhoan.FindAsync(username);
                if (userAccount != null)
                {
                    await _emailService.SendEmailAsync(userAccount.Email, "Xác nhận Đặt trước Sách", userMessage);
                }
                var userNotification = new Notification
                {
                    Username = username,
                    Message = userMessage,
                    CreatedAt = DateTime.Now,
                    IsRead = false
                };
                await _context.Notifications.AddAsync(userNotification);


                await _context.SaveChangesAsync();

                TempData["SuccessMessage"] = "Đã đặt trước sách thành công!";
                return RedirectToAction("BookDetails", new { bookId = bookId });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi đặt trước sách: BookId={BookId}", bookId, ex.Message);
                TempData["ErrorMessage"] = "Có lỗi xảy ra khi đặt trước sách.";
                return RedirectToAction("BookDetails", new { bookId });
            }
        }
        [Authorize(Roles = "DocGia")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> BorrowBook(int bookId)
        {
            try
            {
                var book = await _context.Books.FindAsync(bookId);
                if (book == null || book.SoLuong <= 0)
                {
                    TempData["ErrorMessage"] = "Sách không tồn tại hoặc đã hết.";
                    return RedirectToAction("BookDetails", new { bookId });
                }

                var username = User.Identity.Name;
                // >>> THÊM KHỐI KIỂM TRA SÁCH QUÁ HẠN TẠI ĐÂY <<<
                var hasOverdueBooks = await _context.BookLoans
                    .AnyAsync(l => l.Username == username && l.Status == "Approved" && l.ReturnDate == null && l.DueDate < DateTime.Now);

                if (hasOverdueBooks)
                {
                    TempData["ErrorMessage"] = "Bạn có sách đang mượn quá hạn. Vui lòng trả sách trước khi mượn sách mới.";
                    return RedirectToAction("BookDetails", new { bookId = bookId });
                }
                // Đã có kiểm tra này, đảm bảo không có yêu cầu mượn hoặc đang mượn sách này
                var hasActiveOrPendingLoan = await _context.BookLoans
                    .AnyAsync(l => l.BookId == bookId && l.Username == username && (l.Status == "Pending" || (l.Status == "Approved" && l.ReturnDate == null)));

                if (hasActiveOrPendingLoan)
                {
                    TempData["ErrorMessage"] = "Bạn đã có yêu cầu mượn hoặc đang mượn sách này.";
                    return RedirectToAction("BookDetails", new { bookId });
                }

                // THÊM LOGIC MỚI: Kiểm tra nếu độc giả đã đặt trước sách này
                var hasPendingReservation = await _context.BookReservations
                    .AnyAsync(r => r.BookId == bookId && r.Username == username && r.Status == "Pending");

                if (hasPendingReservation)
                {
                    TempData["ErrorMessage"] = "Bạn đã đặt trước sách này, không thể mượn thêm.";
                    return RedirectToAction("BookDetails", new { bookId });
                }
                // KẾT THÚC LOGIC MỚI

                book.SoLuong--;

                var loan = new BookLoan
                {
                    BookId = bookId,
                    Username = username,
                    BorrowDate = DateTime.Now,
                    Status = "Pending"
                };
                _context.BookLoans.Add(loan);

                var userMessage = $"Bạn đã gửi yêu cầu mượn sách '{book.Title}'. Vui lòng chờ thủ thư duyệt.";
                var userNotification = new Notification
                {
                    Username = username,
                    Message = userMessage,
                    CreatedAt = DateTime.Now
                };
                await _context.Notifications.AddAsync(userNotification);

                // Gửi thông báo cho thủ thư
                var librarianMessage = $"Độc giả '{username}' đã gửi yêu cầu mượn sách '{book.Title}'.";
                var librarians = await _context.TaiKhoan.Where(t => t.LoaiTaiKhoan == "ThuThu").ToListAsync();
                foreach (var librarian in librarians)
                {
                    var librarianNotification = new Notification
                    {
                        Username = librarian.Username,
                        Message = librarianMessage,
                        CreatedAt = DateTime.Now,
                        IsRead = false
                    };
                    await _context.Notifications.AddAsync(librarianNotification);
                }

                await _context.SaveChangesAsync();

                TempData["SuccessMessage"] = "Đã gửi yêu cầu mượn sách thành công!";
                return RedirectToAction("BookDetails", new { bookId });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi gửi yêu cầu mượn sách.");
                TempData["ErrorMessage"] = "Có lỗi xảy ra khi gửi yêu cầu mượn sách.";
                return RedirectToAction("BookDetails", new { bookId });
            }
        }
        [Authorize(Roles = "DocGia")]
        public async Task<IActionResult> LoanStatus()
        {
            var username = User.Identity.Name;

            // Lấy danh sách sách đang mượn
            var currentlyBorrowed = await _context.BookLoans
                .Include(l => l.Book)
                .Where(l => l.Username == username && l.Status == "Approved" && l.ReturnDate == null)
                .OrderBy(l => l.DueDate)
                .ToListAsync();

            // Lấy danh sách sách đang đặt trước
            var pendingReservations = await _context.BookReservations
                .Include(r => r.Book)
                .Where(r => r.Username == username && r.Status == "Pending")
                .OrderBy(r => r.ReservationDate)
                .ToListAsync();

            var viewModel = new ReaderStatusViewModel
            {
                CurrentlyBorrowed = currentlyBorrowed,
                PendingReservations = pendingReservations
            };

            return View(viewModel);
        }
        [Authorize(Roles = "DocGia")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CancelReservation(int reservationId)
        {
            var username = User.Identity.Name;
            var reservation = await _context.BookReservations
                .FirstOrDefaultAsync(r => r.ReservationId == reservationId && r.Username == username);

            if (reservation != null)
            {
                reservation.Status = "Cancelled"; // <-- Trạng thái mới cho độc giả tự hủy
                await _context.SaveChangesAsync();
                TempData["SuccessMessage"] = "Đã hủy yêu cầu đặt trước thành công.";
            }
            else
            {
                TempData["ErrorMessage"] = "Không tìm thấy yêu cầu đặt trước.";
            }

            return RedirectToAction("LoanStatus");
        }

        [Authorize]
        public async Task<IActionResult> BookDetails(int bookId)
        {
            var book = await _context.Books
                .Include(b => b.BookImages)
                .FirstOrDefaultAsync(b => b.BookId == bookId);

            if (book == null)
            {
                _logger.LogWarning("Không tìm thấy sách với ID: {BookId}", bookId);
                return NotFound("Không tìm thấy sách.");
            }

            _logger.LogInformation("Sách: {Title}, Số ảnh: {ImageCount}", book.Title, book.BookImages?.Count ?? 0);
            return View(book);
        }

        [Authorize]
        public async Task<IActionResult> BookList(string searchString, string genre)
        {
            var books = _context.Books
                .Include(b => b.BookImages)
                .AsQueryable();

            if (!string.IsNullOrEmpty(searchString))
            {
                books = books.Where(b => b.Title.Contains(searchString) || b.Author.Contains(searchString));
            }
            if (!string.IsNullOrEmpty(genre))
            {
                books = books.Where(b => b.Genre == genre);
            }

            var bookList = await books.ToListAsync();
            _logger.LogInformation("Tổng số sách: {Count}", bookList.Count);
            foreach (var book in bookList)
            {
                _logger.LogInformation("Sách: {Title}, Số lượng: {SoLuong}, Số ảnh: {ImageCount}",
                    book.Title, book.SoLuong, book.BookImages?.Count ?? 0);
            }

            return View(bookList);
        }
        /// <summary>
        /// [ĐÃ SỬA] Phương thức cho trang xem tất cả thông báo. Chỉ lấy danh sách, không tự động cập nhật.
        /// </summary>
        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        [Authorize(Roles = "DocGia")]
        public async Task<IActionResult> Notifications()
        {
            try
            {
                var username = User.Identity.Name;
                if (string.IsNullOrEmpty(username))
                {
                    return Unauthorized("Vui lòng đăng nhập để xem thông báo.");
                }

                var notifications = await _context.Notifications
                    .Where(n => n.Username == username)
                    .OrderByDescending(n => n.CreatedAt)
                    .ToListAsync();

                return View("ReaderNotifications", notifications);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi tải thông báo: {Message}", ex.Message);
                TempData["ErrorMessage"] = "Có lỗi xảy ra khi tải thông báo.";
                return View("ReaderNotifications", new List<Notification>());
            }
        }

        /// <summary>
        /// [MỚI] API để lấy dữ liệu cho dropdown thông báo động.
        /// </summary>
        [HttpGet]
        [Route("api/notifications")]
        [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
        public async Task<IActionResult> GetNotificationsData()
        {
            if (!User.Identity.IsAuthenticated)
            {
                return Unauthorized();
            }

            var username = User.Identity.Name;

            var notifications = await _context.Notifications
                .Where(n => n.Username == username)
                .OrderByDescending(n => n.CreatedAt)
                .Take(10)
                .ToListAsync();

            var unreadCount = await _context.Notifications
                .CountAsync(n => n.Username == username && !n.IsRead);

            return Json(new { notifications, unreadCount });
        }

        /// <summary>
        /// [MỚI] API để đánh dấu một thông báo là đã đọc.
        /// </summary>
        [HttpPost]
        [Route("api/notifications/mark-as-read/{notificationId}")]
        public async Task<IActionResult> MarkAsRead(int notificationId)
        {
            if (!User.Identity.IsAuthenticated)
            {
                return Unauthorized();
            }

            var username = User.Identity.Name;
            var notification = await _context.Notifications
                .FirstOrDefaultAsync(n => n.NotificationId == notificationId && n.Username == username);

            if (notification == null)
            {
                return NotFound();
            }

            if (!notification.IsRead)
            {
                notification.IsRead = true;
                await _context.SaveChangesAsync();
            }

            return Ok();
        }

        /// <summary>
        /// [MỚI] Phương thức để xử lý nút "Đánh dấu tất cả đã đọc"
        /// </summary>
        [HttpPost]
        [Authorize(Roles = "DocGia")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> MarkAllAsRead()
        {
            var username = User.Identity.Name;
            var unreadNotifications = await _context.Notifications
                .Where(n => n.Username == username && !n.IsRead)
                .ToListAsync();

            if (unreadNotifications.Any())
            {
                foreach (var notification in unreadNotifications)
                {
                    notification.IsRead = true;
                }
                await _context.SaveChangesAsync();
            }

            return RedirectToAction("Notifications");
        }

    }
}