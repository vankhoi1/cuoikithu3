using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using QuanLyThuVien.Data;
using QuanLyThuVien.Models;
using QuanLyThuVien.Services;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using ClosedXML.Excel;
using QuanLyThuVien;
namespace QuanLyThuVien.Controllers
{
    [Authorize(Roles = "ThuThu")]
    public class LibrarianController : Controller
    {
        private readonly LibraryDbContext _context;
        private readonly GmailEmailService _emailService;
        private readonly ILogger<LibrarianController> _logger;
        private readonly OnnxImageService _onnxImageService;
        public LibrarianController(LibraryDbContext context, GmailEmailService emailService, ILogger<LibrarianController> logger ,OnnxImageService onnxImageService)
        {
            _context = context;
            _emailService = emailService;
            _logger = logger;
            _onnxImageService = onnxImageService;
        }

        public async Task<IActionResult> Index()
        {
            // Lấy số yêu cầu mượn sách đang chờ
            var pendingLoanCount = await _context.BookLoans
                .CountAsync(l => l.Status == "Pending");

            // Lấy số yêu cầu gia hạn đang chờ
            var pendingExtensionCount = await _context.Notifications
                .CountAsync(n => n.Message.StartsWith("YEUCAUGIAHANSACHDOCGIA:") && !n.IsRead);

            // Lấy số yêu cầu đặt trước đang chờ
            var pendingReservationCount = await _context.BookReservations
                .CountAsync(r => r.Status == "Pending");

            ViewData["PendingLoanCount"] = pendingLoanCount + pendingExtensionCount;
            ViewData["PendingReservationCount"] = pendingReservationCount;

            // --- THÊM MỚI QUAN TRỌNG ---
            // Lấy danh sách *tất cả* độc giả đã từng nhắn tin cho thủ thư
            var readers = await _context.ChatMessages
                .Where(m => m.ToUser == "thu-thu") // Chỉ lấy tin nhắn gửi cho thủ thư
                .Select(m => m.FromUser)
                .Distinct()
                .ToListAsync();

            // Gửi danh sách này sang View
            ViewBag.Readers = readers;
            // --- KẾT THÚC THÊM MỚI ---

            return View();
        }



        #region Book Management (CRUD)
        public async Task<IActionResult> ManageBooks()
        {
            var books = await _context.Books
                .Include(b => b.BookImages)
                .ToListAsync();
            return View(books);
        }

        public async Task<IActionResult> CreateBook()
        {
            ViewBag.Genres = new SelectList(await _context.Genres.ToListAsync(), "Name", "Name");
            return View();
        }
        // >>> THÊM PHƯƠNG THỨC NÀY VÀO TRONG CLASS <<<
        private async Task LogActivity(string action, string? details = null)
        {
            var username = User.Identity.Name;
            if (string.IsNullOrEmpty(username)) return;

            var activity = new LibrarianActivity
            {
                Username = username,
                Action = action,
                Details = details,
                Timestamp = DateTime.Now
            };
            _context.LibrarianActivities.Add(activity);
        }
        // >>> THÊM PHƯƠNG THỨC NÀY VÀO TRONG CLASS LIBRARIANCONTROLLER <<<
        [Authorize(Roles = "ThuThu")]
        public async Task<IActionResult> MyActivityHistory()
        {
            // Lấy username của thủ thư đang đăng nhập
            var username = User.Identity.Name;

            if (string.IsNullOrEmpty(username))
            {
                // Xử lý trường hợp không xác định được người dùng
                return Forbid();
            }

            // Truy vấn các hoạt động của chính người dùng này
            var activities = await _context.LibrarianActivities
                .Where(a => a.Username == username)
                .OrderByDescending(a => a.Timestamp)
                .ToListAsync();

            // Trả về View cùng với danh sách hoạt động
            return View(activities);
        }
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateBook(Book book, List<IFormFile> images)
        {
            if (!string.IsNullOrEmpty(book.Genre))
            {
                var existingGenre = await _context.Genres.FirstOrDefaultAsync(g => g.Name == book.Genre);
                if (existingGenre == null)
                {
                    // Nếu thể loại chưa tồn tại, tạo mới nó
                    var newGenre = new Genre { Name = book.Genre };
                    _context.Genres.Add(newGenre);
                    // Không cần SaveChangesAsync ở đây, nó sẽ được lưu cùng với sách
                }
            }
            if (ModelState.IsValid)
            {
                var uploadsFolder = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot/BookImages");
                if (!Directory.Exists(uploadsFolder))
                    Directory.CreateDirectory(uploadsFolder);

                if (images != null && images.Count > 0)
                {
                    var firstImage = images[0];
                    var uniqueFileName = Guid.NewGuid().ToString() + Path.GetExtension(firstImage.FileName);
                    var filePath = Path.Combine(uploadsFolder, uniqueFileName);

                    using (var stream = new FileStream(filePath, FileMode.Create))
                    {
                        await firstImage.CopyToAsync(stream);
                    }
                    book.CoverImagePath = "/BookImages/" + uniqueFileName;
                }

                _context.Books.Add(book);
                await LogActivity("Thêm sách mới", $"Sách: {book.Title}");
                await _context.SaveChangesAsync();

                if (images != null && images.Count > 0)
                {
                    foreach (var image in images)
                    {
                        if (image.Length > 0)
                        {
                            var uniqueFileName = Guid.NewGuid().ToString() + Path.GetExtension(image.FileName);
                            var filePath = Path.Combine(uploadsFolder, uniqueFileName);

                            using (var stream = new FileStream(filePath, FileMode.Create))
                            {
                                await image.CopyToAsync(stream);
                            }

                            var bookImage = new BookImage
                            {
                                BookId = book.BookId,
                                ImagePath = "/BookImages/" + uniqueFileName,
                                ImageUrl = "/BookImages/" + uniqueFileName
                            };
                            // >>> BẮT ĐẦU TÍNH TOÁN VÀ LƯU VECTOR <<<
                            using (var memoryStream = new MemoryStream())
                            {
                                await image.CopyToAsync(memoryStream);
                                memoryStream.Position = 0; // Quan trọng: Reset vị trí stream về đầu
                                var vector = await _onnxImageService.GetImageVectorAsync(memoryStream);
                                if (vector != null)
                                {
                                    // Chuyển mảng float[] thành byte[] để lưu vào DB
                                    var vectorBytes = new byte[vector.Length * 4];
                                    Buffer.BlockCopy(vector, 0, vectorBytes, 0, vectorBytes.Length);
                                    bookImage.ImageVector = vectorBytes;
                                }
                            }
                            // >>> KẾT THÚC TÍNH TOÁN VÀ LƯU VECTOR <<<

                            _context.BookImages.Add(bookImage);
                        }
                    }
                    await _context.SaveChangesAsync();
                }

                TempData["SuccessMessage"] = $"Đã thêm thành công sách: {book.Title}";
                return RedirectToAction(nameof(ManageBooks));
            }

            ViewBag.Genres = new SelectList(await _context.Genres.ToListAsync(), "Name", "Name", book.Genre);
            return View(book);
        }

        public async Task<IActionResult> EditBook(int? id)
        {
            if (id == null) return NotFound();
            var book = await _context.Books.FindAsync(id);
            if (book == null) return NotFound();

            ViewBag.Genres = new SelectList(await _context.Genres.ToListAsync(), "Name", "Name", book.Genre);
            return View(book);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditBook(int id, Book book, IFormFile? imageFile)
        {
            if (id != book.BookId)
            {
                return NotFound();
            }

            // Tải đối tượng sách gốc từ database, BAO GỒM CẢ DANH SÁCH ẢNH CŨ
            var bookToUpdate = await _context.Books
                .Include(b => b.BookImages) // TẢI KÈM DỮ LIỆU ẢNH CŨ
                .FirstOrDefaultAsync(b => b.BookId == id);

            if (bookToUpdate == null)
            {
                return NotFound();
            }

            // (Phần code xử lý Genre và ModelState giữ nguyên như các lần sửa trước)
            if (!string.IsNullOrEmpty(book.Genre))
            {
                var existingGenre = await _context.Genres.FirstOrDefaultAsync(g => g.Name == book.Genre);
                if (existingGenre == null)
                {
                    _context.Genres.Add(new Genre { Name = book.Genre });
                }
            }

            if (ModelState.IsValid)
            {
                // Cập nhật các thuộc tính văn bản
                bookToUpdate.Title = book.Title;
                bookToUpdate.Author = book.Author;
                bookToUpdate.Genre = book.Genre;
                bookToUpdate.PublicationYear = book.PublicationYear;
                bookToUpdate.Description = book.Description;
                bookToUpdate.SoLuong = book.SoLuong;

                try
                {
                    await LogActivity("Cập nhật thông tin sách", $"Sách: {bookToUpdate.Title} (ID: {bookToUpdate.BookId})");
                    // Xử lý ảnh nếu có file mới được tải lên
                    if (imageFile != null && imageFile.Length > 0)
                    {
                        var uploadsFolder = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot/BookImages");
                        if (!Directory.Exists(uploadsFolder)) Directory.CreateDirectory(uploadsFolder);

                        var uniqueFileName = Guid.NewGuid().ToString() + Path.GetExtension(imageFile.FileName);
                        var filePath = Path.Combine(uploadsFolder, uniqueFileName);

                        using (var stream = new FileStream(filePath, FileMode.Create))
                        {
                            await imageFile.CopyToAsync(stream);
                        }

                        // Cập nhật đường dẫn ảnh bìa chính
                        bookToUpdate.CoverImagePath = "/BookImages/" + uniqueFileName;

                        // ================== LOGIC MỚI QUAN TRỌNG ==================
                        // 1. Xóa tất cả các bản ghi ảnh cũ của sách này trong bảng BookImages
                        if (bookToUpdate.BookImages.Any())
                        {
                            // (Tùy chọn) Xóa file vật lý của các ảnh cũ
                            foreach (var oldImg in bookToUpdate.BookImages)
                            {
                                var oldImagePath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", oldImg.ImagePath.TrimStart('/'));
                                if (System.IO.File.Exists(oldImagePath))
                                {
                                    System.IO.File.Delete(oldImagePath);
                                }
                            }
                            _context.BookImages.RemoveRange(bookToUpdate.BookImages);
                        }

                        // 2. Thêm bản ghi ảnh mới vào bảng BookImages
                        var newBookImage = new BookImage
                        {
                            BookId = bookToUpdate.BookId,
                            ImagePath = "/BookImages/" + uniqueFileName,
                            ImageUrl = "/BookImages/" + uniqueFileName // Đảm bảo cả hai cột đều có dữ liệu
                        };
                        // >>> BẮT ĐẦU TÍNH TOÁN VÀ LƯU VECTOR <<<
                        using (var memoryStream = new MemoryStream())
                        {
                            // Quan trọng: Phải reset vị trí của imageFile về đầu trước khi đọc lại
                           
                            await imageFile.CopyToAsync(memoryStream);
                            memoryStream.Position = 0;

                            var vector = await _onnxImageService.GetImageVectorAsync(memoryStream);
                            if (vector != null)
                            {
                                var vectorBytes = new byte[vector.Length * 4];
                                Buffer.BlockCopy(vector, 0, vectorBytes, 0, vectorBytes.Length);
                                newBookImage.ImageVector = vectorBytes;
                            }
                        }
                        // >>> KẾT THÚC TÍNH TOÁN VÀ LƯU VECTOR <<<
                        _context.BookImages.Add(newBookImage);
                        // =========================================================
                    }

                    await _context.SaveChangesAsync();
                    TempData["SuccessMessage"] = $"Đã cập nhật thành công sách: {bookToUpdate.Title}";
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!_context.Books.Any(e => e.BookId == book.BookId)) return NotFound();
                    else throw;
                }
                return RedirectToAction(nameof(ManageBooks));
            }

            ViewBag.Genres = new SelectList(await _context.Genres.ToListAsync(), "Name", "Name", book.Genre);
            return View(book);
        }
        public async Task<IActionResult> DeleteBook(int? id)
        {
            if (id == null) return NotFound();
            var book = await _context.Books.FirstOrDefaultAsync(m => m.BookId == id);
            if (book == null) return NotFound();
            return View(book);
        }

        [HttpPost, ActionName("DeleteBook")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteBookConfirmed(int id)
        {
            var book = await _context.Books
                .Include(b => b.BookLoans) // Giả định có quan hệ với BookLoans
                .FirstOrDefaultAsync(b => b.BookId == id);

            if (book == null)
            {
                TempData["ErrorMessage"] = "Cuốn sách không tồn tại.";
                return RedirectToAction("ManageBooks", "Librarian"); // Giả định quay về danh sách sách
            }

            // Kiểm tra và xóa các khoản vay liên quan trong BookLoans
            var relatedLoans = await _context.BookLoans
                .Where(bl => bl.BookId == id)
                .ToListAsync();
            if (relatedLoans.Any())
            {
                _context.BookLoans.RemoveRange(relatedLoans);
                await _context.SaveChangesAsync();
            }
            await LogActivity("Xóa sách", $"Sách: '{book.Title}' (ID: {id})");
            // Xóa cuốn sách
            _context.Books.Remove(book);
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = $"Cuốn sách '{book.Title}' đã được xóa thành công.";
            _logger.LogInformation("Cuốn sách {BookId} ({Title}) đã được xóa bởi {CurrentUser}.", id, book.Title, User.Identity.Name);
            return RedirectToAction("ManageBooks", "Librarian");
        }
        #endregion

        #region Loan and Reservation Management
        public async Task<IActionResult> LoanDashboard()
        {
            var pendingLoans = await _context.BookLoans.Include(l => l.Book)
                .Where(l => l.Status == "Pending").OrderBy(l => l.BorrowDate).ToListAsync();

            var activeLoans = await _context.BookLoans.Include(l => l.Book)
                .Where(l => l.Status == "Approved" && l.ReturnDate == null).OrderBy(l => l.DueDate).ToListAsync();

            // **LOGIC LẤY YÊU CẦU GIA HẠN ĐÃ ĐƯỢC CẢI TIẾN**
            var extensionRequestNotifications = await _context.Notifications
                .Where(n => n.Message.StartsWith("YEUCAUGIAHANSACHDOCGIA:") && !n.IsRead && n.LoanId.HasValue)
                .OrderBy(n => n.CreatedAt)
                .ToListAsync();

            var extensionRequestsViewModel = new List<ExtensionRequestViewModel>();
            foreach (var notification in extensionRequestNotifications)
            {
                var loan = await _context.BookLoans.Include(l => l.Book).FirstOrDefaultAsync(l => l.LoanId == notification.LoanId.Value);
                if (loan != null)
                {
                    extensionRequestsViewModel.Add(new ExtensionRequestViewModel
                    {
                        NotificationId = notification.NotificationId,
                        LoanId = loan.LoanId,
                        BookTitle = loan.Book.Title,
                        Username = loan.Username,
                        RequestDate = notification.CreatedAt,
                        DueDate = loan.DueDate,
                        NewDueDate = loan.DueDate?.AddDays(7)
                    });
                }
            }

            var viewModel = new LoanDashboardViewModel
            {
                PendingLoans = pendingLoans,
                ActiveLoans = activeLoans,
                ExtensionRequests = extensionRequestsViewModel
            };

            return View(viewModel);
        }
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ApproveLoan(int loanId, string returnUrl)
        {
            var loan = await _context.BookLoans
                .Include(l => l.Book)
                .Include(l => l.TaiKhoan)
                .FirstOrDefaultAsync(l => l.LoanId == loanId && l.Status == "Pending");

            if (loan == null) return NotFound();

            loan.Status = "Approved";
            // SỬA LỖI THỜI GIAN: Dùng UtcNow
            loan.DueDate = DateTime.Now.AddDays(14);

            var notification = new Notification
            {
                Username = loan.Username,
                Message = $"Yêu cầu mượn sách '{loan.Book.Title}' của bạn đã được duyệt. Hạn trả: {loan.DueDate.Value.ToLocalTime():dd/MM/yyyy}.",
                // SỬA LỖI THỜI GIAN: Dùng UtcNow
                CreatedAt = DateTime.Now
            };
            await _context.Notifications.AddAsync(notification);
            await _context.SaveChangesAsync();
            await _emailService.SendEmailAsync(loan.TaiKhoan.Email, "Yêu cầu mượn sách được duyệt", notification.Message);
            await LogActivity("Duyệt yêu cầu mượn", $"Độc giả: {loan.Username}, Sách: '{loan.Book.Title}'");
            TempData["SuccessMessage"] = "Đã phê duyệt yêu cầu mượn sách.";
           

            // Kiểm tra an toàn và chuyển hướng về lại trang đó
            if (Url.IsLocalUrl(returnUrl))
            {
                return Redirect(returnUrl);
            }
            return RedirectToAction(nameof(LoanDashboard));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RejectLoan(int loanId, string returnUrl)
        {
            using (var transaction = await _context.Database.BeginTransactionAsync())
            {
                try
                {
                    var loan = await _context.BookLoans
                        .Include(l => l.Book)
                        .Include(l => l.TaiKhoan)
                        .FirstOrDefaultAsync(l => l.LoanId == loanId && l.Status == "Pending");

                    if (loan == null)
                    {
                        TempData["ErrorMessage"] = "Không tìm thấy yêu cầu mượn.";
                        return RedirectToAction(nameof(LoanDashboard));
                    }

                    loan.Status = "Rejected";
                    if (loan.Book != null)
                    {
                        loan.Book.SoLuong++; // Cộng trả lại số lượng sách
                    }
                    await LogActivity("Từ chối yêu cầu mượn", $"Độc giả: {loan.Username}, Sách: '{loan.Book?.Title ?? "[Sách đã xóa]"}'");

                    var notification = new Notification
                    {
                        Username = loan.Username,
                        Message = $"Yêu cầu mượn sách '{loan.Book?.Title ?? "[Sách đã bị xóa]"}' của bạn đã bị từ chối.",
                        CreatedAt = DateTime.Now,
                        IsRead = false
                    };
                    await _context.Notifications.AddAsync(notification);

                    await _context.SaveChangesAsync();
                    await transaction.CommitAsync();

                    await _emailService.SendEmailAsync(loan.TaiKhoan.Email, "Yêu cầu mượn sách bị từ chối", notification.Message);
                    TempData["SuccessMessage"] = $"Đã từ chối yêu cầu mượn sách: {loan.Book?.Title ?? "[Sách đã bị xóa]"}";
                }
                catch (Exception ex)
                {
                    await transaction.RollbackAsync();
                    _logger.LogError(ex, "Lỗi khi từ chối yêu cầu mượn: {Message}", ex.Message);
                    TempData["ErrorMessage"] = "Có lỗi xảy ra khi từ chối yêu cầu mượn.";
                }
            }
        

            // Kiểm tra an toàn và chuyển hướng về lại trang đó
            if (Url.IsLocalUrl(returnUrl))
            {
                return Redirect(returnUrl);
            }
            return RedirectToAction(nameof(LoanDashboard));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ReturnBook(int loanId, string returnUrl)
        {
            var loan = await _context.BookLoans
                .Include(l => l.Book)
                .Include(l => l.TaiKhoan)
                .FirstOrDefaultAsync(l => l.LoanId == loanId && l.Status == "Approved" && l.ReturnDate == null);

            if (loan == null) return NotFound();

            // SỬA LỖI THỜI GIAN: Dùng UtcNow
            loan.ReturnDate = DateTime.Now;
            loan.Status = "Returned";
            loan.Book.SoLuong++;
            await LogActivity("Xác nhận trả sách", $"Độc giả: {loan.Username}, Sách: '{loan.Book.Title}'");
            var returnNotification = new Notification
            {
                Username = loan.Username,
                Message = $"Bạn đã trả sách '{loan.Book.Title}' thành công vào ngày {loan.ReturnDate.Value.ToLocalTime():dd/MM/yyyy}.",
                // SỬA LỖI THỜI GIAN: Dùng UtcNow
                CreatedAt = DateTime.Now,
            };
            await _context.Notifications.AddAsync(returnNotification);

            // Check for reservations
            var nextReservation = await _context.BookReservations
               .Where(r => r.BookId == loan.BookId && r.Status == "Pending")
                .OrderBy(r => r.ReservationDate)
                .FirstOrDefaultAsync();

            // ================== BẮT ĐẦU LOGIC THAY ĐỔI ==================
            if (nextReservation != null)
            {
                // Sách vừa được trả (+1) giờ lại được xử lý cho người đặt trước,
                // số lượng sách sẽ được trừ đi khi phiếu mượn mới được duyệt,
                // nên ở đây ta không cần thay đổi SoLuong nữa.
                loan.Book.SoLuong--;
                // Đánh dấu yêu cầu đặt trước này đã được xử lý
                nextReservation.Status = "Fulfilled"; // Trạng thái mới: Đã hoàn thành

                // TẠO MỘT PHIẾU MƯỢN MỚI cho người đã đặt trước
                var newLoanForReserver = new BookLoan
                {
                    BookId = nextReservation.BookId,
                    Username = nextReservation.Username,
                    BorrowDate = DateTime.Now,
                    Status = "Pending" // Ở trạng thái chờ thủ thư duyệt
                };
                await _context.BookLoans.AddAsync(newLoanForReserver);

                // Gửi thông báo cho người đặt trước
                var userNotification = new Notification
                {
                    Username = nextReservation.Username,
                    Message = $"Sách '{loan.Book.Title}' bạn đặt trước đã được chuyển thành yêu cầu mượn. Vui lòng chờ thủ thư duyệt.",
                    CreatedAt = DateTime.Now,
                };
                await _context.Notifications.AddAsync(userNotification);
            }
            // =================== KẾT THÚC LOGIC THAY ĐỔI ===================


            await _context.SaveChangesAsync();
            TempData["SuccessMessage"] = "Đã xử lý trả sách thành công.";
            
            // Kiểm tra an toàn và chuyển hướng về lại trang đó
            if (Url.IsLocalUrl(returnUrl))
            {
                return Redirect(returnUrl);
            }
            return RedirectToAction(nameof(LoanDashboard));
        }
        public async Task<IActionResult> ManageReservations()
        {
            var reservations = await _context.BookReservations
                .Include(r => r.Book)
                .Include(r => r.TaiKhoan)
                .Where(r => r.Status == "Pending")
                .OrderBy(r => r.ReservationDate)
                .ToListAsync();
            return View(reservations);
        }

       

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RejectReservation(int reservationId , string sourceUsername, string returnUrl)
        {
            try
            {
                var reservation = await _context.BookReservations
                    .Include(r => r.Book)
                    .Include(r => r.TaiKhoan)
                    .FirstOrDefaultAsync(r => r.ReservationId == reservationId && r.Status == "Pending");

                if (reservation == null)
                {
                    TempData["ErrorMessage"] = "Không tìm thấy đặt trước.";
                    return RedirectToAction(nameof(ManageReservations));
                }

                reservation.Status = "Rejected";
                var notification = new Notification
                {
                    Username = reservation.Username,
                    Message = $"Yêu cầu đặt trước sách '{reservation.Book.Title}' của bạn đã bị từ chối.",
                    CreatedAt = DateTime.Now,
                    IsRead = false
                };
                await _context.Notifications.AddAsync(notification);

                await _emailService.SendEmailAsync(
                    reservation.TaiKhoan.Email,
                    "Yêu cầu đặt trước bị từ chối",
                    notification.Message);

                await _context.SaveChangesAsync();
                TempData["SuccessMessage"] = $"Đã từ chối đặt trước sách: {reservation.Book.Title}";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi từ chối đặt trước: {Message}", ex.Message);
                TempData["ErrorMessage"] = "Có lỗi xảy ra khi từ chối đặt trước: " + ex.Message;
            }
            if (!string.IsNullOrEmpty(sourceUsername))
            {
                return RedirectToAction("ReaderDetails", new { username = sourceUsername });
            }
         

            // Kiểm tra an toàn và chuyển hướng về lại trang đó
            if (Url.IsLocalUrl(returnUrl))
            {
                return Redirect(returnUrl);
            }
            return RedirectToAction(nameof(ManageReservations));
           
        }
        #endregion

        #region Genre Management
        public async Task<IActionResult> ManageGenres()
        {
            var genres = await _context.Genres.ToListAsync();
            return View(genres);
        }

        public IActionResult CreateGenre()
        {
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateGenre(Genre genre)
        {
            if (ModelState.IsValid)
            {
                if (await _context.Genres.AnyAsync(g => g.Name == genre.Name))
                {
                    ModelState.AddModelError("Name", "Tên thể loại này đã tồn tại.");
                    return View(genre);
                }
                await LogActivity("Thêm thể loại mới", $"Thể loại: '{genre.Name}'");
                _context.Add(genre);
                await _context.SaveChangesAsync();
                TempData["SuccessMessage"] = "Đã thêm thể loại thành công!";
                return RedirectToAction(nameof(ManageGenres));
            }
            return View(genre);
        }

        // Xóa thể loại
        public async Task<IActionResult> DeleteGenre(int id)
        {
            var genre = await _context.Genres.FindAsync(id);
            if (genre == null)
            {
                TempData["ErrorMessage"] = "Thể loại không tồn tại.";
                return RedirectToAction(nameof(ManageGenres));
            }
            return View(genre);
        }

        [HttpPost, ActionName("DeleteGenre")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteGenreConfirmed(int id)
        {
            var genre = await _context.Genres.FindAsync(id);
            if (genre == null)
            {
                TempData["ErrorMessage"] = "Thể loại không tồn tại.";
                return RedirectToAction(nameof(ManageGenres));
            }

            // Kiểm tra liên kết với sách
            var hasBooks = await _context.Books.AnyAsync(b => b.Genre == genre.Name); // Sử dụng Genre (chuỗi) thay vì GenreId
            if (hasBooks)
            {
                TempData["ErrorMessage"] = "Không thể xóa thể loại vì còn liên kết với sách. Vui lòng cập nhật sách liên quan trước.";
                return RedirectToAction(nameof(ManageGenres));
            }
            await LogActivity("Xóa thể loại", $"Thể loại: '{genre.Name}' (ID: {id})");
            _context.Genres.Remove(genre);
            await _context.SaveChangesAsync();
            TempData["SuccessMessage"] = $"Đã xóa thể loại '{genre.Name}' thành công!";
            return RedirectToAction(nameof(ManageGenres));
        }

        // Sửa thể loại
        public async Task<IActionResult> EditGenre(int id)
        {
            var genre = await _context.Genres.FindAsync(id);
            if (genre == null)
            {
                TempData["ErrorMessage"] = "Thể loại không tồn tại.";
                return RedirectToAction(nameof(ManageGenres));
            }
            return View(genre);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditGenre(int id, Genre genre)
        {
            if (id != genre.GenreId)
            {
                TempData["ErrorMessage"] = "ID thể loại không khớp.";
                return RedirectToAction(nameof(ManageGenres));
            }

            if (ModelState.IsValid)
            {
                // Kiểm tra trùng lặp, trừ trường hợp chỉnh sửa chính nó
                var existingGenre = await _context.Genres.FirstOrDefaultAsync(g => g.Name == genre.Name && g.GenreId != id);
                if (existingGenre != null)
                {
                    ModelState.AddModelError("Name", "Tên thể loại này đã tồn tại.");
                    return View(genre);
                }
                await LogActivity("Cập nhật thể loại", $"Thể loại: '{genre.Name}' (ID: {id})");
                _context.Update(genre);
                await _context.SaveChangesAsync();
                TempData["SuccessMessage"] = $"Đã cập nhật thể loại '{genre.Name}' thành công!";
                return RedirectToAction(nameof(ManageGenres));
            }
            return View(genre);
        }
        #endregion

        #region Reader Management and Statistics
        public async Task<IActionResult> ManageReaders()
        {
            var readers = await _context.TaiKhoan
                .Where(u => u.LoaiTaiKhoan == "DocGia")
                .Select(u => new AccountViewModel
                {
                    Username = u.Username,
                    Email = u.Email,
                    Roles = new List<string> { u.LoaiTaiKhoan },
                    IsActive = u.IsActive
                })
                .ToListAsync();
            return View(readers);
        }


        public async Task<IActionResult> ReaderDetails(string username)
        {
            if (string.IsNullOrEmpty(username)) return NotFound();

            var taiKhoan = await _context.TaiKhoan.FirstOrDefaultAsync(tk => tk.Username == username);
            if (taiKhoan == null)
            {
                return NotFound("Không tìm thấy tài khoản hoặc độc giả này.");
            }

            var docGia = await _context.DocGia
                .FirstOrDefaultAsync(dg => dg.Username == username);

            if (docGia == null && taiKhoan.LoaiTaiKhoan == "DocGia")
            {
                _logger.LogWarning($"Tài khoản {username} là độc giả nhưng không có thông tin DocGia.");
                return NotFound("Thông tin độc giả không đầy đủ.");
            }

            var retrievedBorrowedBooks = await _context.BookLoans
                .Include(l => l.Book)
                .Where(l => l.Username == username && l.Status == "Approved" && l.ReturnDate == null)
                .ToListAsync();

            var retrievedPendingReservations = await _context.BookReservations
                .Include(r => r.Book)
                .Where(r => r.Username == username && r.Status == "Pending")
                .ToListAsync();

            var retrievedPendingLoanRequests = await _context.BookLoans
                .Include(l => l.Book)
                .Where(l => l.Username == username && l.Status == "Pending")
                .ToListAsync();

            // ================= BẮT ĐẦU PHẦN LOGIC MỚI =================
            //  Logic lấy yêu cầu gia hạn đang chờ duyệt, đồng bộ với trang LoanDashboard

            // 1. Tìm tất cả các ID phiếu mượn đang hoạt động của độc giả này
            var userLoanIds = await _context.BookLoans
                .Where(l => l.Username == username && l.Status == "Approved" && l.ReturnDate == null)
                .Select(l => l.LoanId)
                .ToListAsync();

            // 2. Tìm các thông báo yêu cầu gia hạn gốc, chưa đọc, gửi cho thủ thư và liên quan đến các phiếu mượn trên
            var extensionRequestNotifications = await _context.Notifications
                .Include(n => n.TaiKhoan)
                .Where(n => n.TaiKhoan.LoaiTaiKhoan == "ThuThu" &&
                            n.Message.StartsWith("YEUCAUGIAHANSACHDOCGIA:") &&
                            !n.IsRead &&
                            n.LoanId.HasValue &&
                            userLoanIds.Contains(n.LoanId.Value))
                .OrderByDescending(n => n.CreatedAt)
                .ToListAsync();

            // 3. Chuyển đổi dữ liệu sang ViewModel để hiển thị
            var pendingExtensionRequestsViewModel = new List<ExtensionRequestViewModel>();
            foreach (var notification in extensionRequestNotifications)
            {
                var loan = await _context.BookLoans
                    .Include(l => l.Book)
                    .FirstOrDefaultAsync(l => l.LoanId == notification.LoanId.Value);

                if (loan != null)
                {
                    pendingExtensionRequestsViewModel.Add(new ExtensionRequestViewModel
                    {
                        NotificationId = notification.NotificationId,
                        LoanId = loan.LoanId,
                        BookTitle = loan.Book.Title,
                        Username = loan.Username,
                        RequestDate = notification.CreatedAt,
                        DueDate = loan.DueDate,
                        NewDueDate = loan.DueDate?.AddDays(7)
                    });
                }
            }
            // ================= KẾT THÚC PHẦN LOGIC MỚI =================

            var viewModel = new ReaderDetailsViewModel
            {
                Username = taiKhoan.Username,
                HoTen = docGia?.HoTen,
                Email = taiKhoan.Email,
                SoDienThoai = docGia?.SoDienThoai,
                NgaySinh = docGia?.NgaySinh,
                DiaChi = docGia?.DiaChi,
                IsActive = taiKhoan.IsActive,

                CurrentlyBorrowedBooks = retrievedBorrowedBooks,
                PendingReservations = retrievedPendingReservations,
                PendingLoanRequests = retrievedPendingLoanRequests,

                // Gán danh sách yêu cầu gia hạn đã được xử lý bằng logic mới
                PendingExtensionRequests = pendingExtensionRequestsViewModel
            };

            return View(viewModel);
        }

        public async Task<IActionResult> ReaderHistory(string username)
        {
            if (string.IsNullOrEmpty(username))
            {
                return NotFound();
            }

            ViewData["ReaderUsername"] = username; // Gửi username qua view để hiển thị

            var loanHistory = await _context.BookLoans
                .Include(l => l.Book)
                .Where(l => l.Username == username)
                .OrderByDescending(l => l.BorrowDate)
                .ToListAsync();

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

            // Chúng ta sẽ tạo một View mới tên là ReaderHistory.cshtml
            return View("ReaderHistory", viewModel);
        }
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ToggleUserStatus(string username)
        {
            if (string.IsNullOrEmpty(username)) return NotFound();

            var user = await _context.TaiKhoan.FirstOrDefaultAsync(u => u.Username == username);
            if (user != null)
            {
                user.IsActive = !user.IsActive;
                await _context.SaveChangesAsync();
                TempData["SuccessMessage"] = "Đã cập nhật trạng thái tài khoản thành công!";
            }
            return RedirectToAction("ManageReaders");
        }

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
        public async Task<IActionResult> ManageExtensionRequests()
        {
            var extensionRequests = await _context.Notifications
                .Where(n => n.Message.Contains("yêu cầu gia hạn") && !n.IsRead)
                .OrderBy(n => n.CreatedAt)
                .ToListAsync();

            return View(extensionRequests);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ApproveExtension(int notificationId, string returnUrl)
        {
            var notification = await _context.Notifications.FindAsync(notificationId);
            if (notification == null)
            {
                TempData["ErrorMessage"] = "Không tìm thấy thông báo.";
                return RedirectToAction("LoanDashboard");
            }

            // SỬA LỖI: Sử dụng trực tiếp notification.LoanId thay vì phân tích chuỗi
            if (!notification.LoanId.HasValue)
            {
                TempData["ErrorMessage"] = "Thông báo này không chứa mã phiếu mượn hợp lệ.";
                return RedirectToAction("LoanDashboard");
            }

            var loan = await _context.BookLoans
                .Include(l => l.TaiKhoan)
                .Include(l => l.Book)
                .FirstOrDefaultAsync(l => l.LoanId == notification.LoanId.Value);

            if (loan == null)
            {
                TempData["ErrorMessage"] = "Không tìm thấy phiếu mượn liên quan.";
            

                // Kiểm tra an toàn và chuyển hướng về lại trang đó
                if (Url.IsLocalUrl(returnUrl))
                {
                    return Redirect(returnUrl);
                }
                return RedirectToAction("LoanDashboard");
            }

            // Cập nhật ngày hạn trả và trạng thái thông báo
            loan.DueDate = loan.DueDate.HasValue ? loan.DueDate.Value.AddDays(7) : DateTime.Now.AddDays(7);
            notification.IsRead = true;
            await LogActivity("Duyệt gia hạn sách", $"Độc giả: {loan.Username}, Sách: '{loan.Book.Title}', Hạn mới: {loan.DueDate:dd/MM/yyyy}");
            // Tạo thông báo mới xác nhận đã duyệt cho người dùng
            var userNotification = new Notification
            {
                Username = loan.Username,
                Message = $"Yêu cầu gia hạn sách '{loan.Book.Title}' của bạn đã được duyệt. Hạn trả mới: {loan.DueDate:dd/MM/yyyy}.",
                CreatedAt = DateTime.Now,
                LoanId = loan.LoanId
            };
            await _context.Notifications.AddAsync(userNotification);

            // Gửi email cho độc giả (nếu có)
            if (loan.TaiKhoan?.Email != null)
            {
                await _emailService.SendEmailAsync(
                    loan.TaiKhoan.Email,
                    "Yêu cầu gia hạn sách đã được duyệt",
                    userNotification.Message
                );
            }

            await _context.SaveChangesAsync();
            TempData["SuccessMessage"] = $"Đã duyệt gia hạn thành công cho phiếu mượn #{loan.LoanId}.";

            // Bạn có thể thay đổi RedirectToAction cho phù hợp, ví dụ về trang chi tiết độc giả
            return RedirectToAction("LoanDashboard");
        }
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RejectExtension(int notificationId, string returnUrl)
        {
            var notification = await _context.Notifications.FindAsync(notificationId);
            if (notification == null)
            {
                TempData["ErrorMessage"] = "Không tìm thấy yêu cầu gia hạn.";
                return RedirectToAction("LoanDashboard");
            }

            // SỬA LỖI 1: Sử dụng trực tiếp LoanId, không phân tích chuỗi
            if (!notification.LoanId.HasValue)
            {
                TempData["ErrorMessage"] = "Yêu cầu gia hạn không hợp lệ (thiếu mã phiếu mượn).";
                // Xóa thông báo lỗi để tránh làm phiền giao diện
                _context.Notifications.Remove(notification);
                await _context.SaveChangesAsync();
                return RedirectToAction("LoanDashboard");
            }

            var loan = await _context.BookLoans
                .Include(l => l.Book)
                .Include(l => l.TaiKhoan)
                .FirstOrDefaultAsync(l => l.LoanId == notification.LoanId.Value);

            if (loan == null)
            {
                TempData["ErrorMessage"] = "Không tìm thấy phiếu mượn được liên kết với yêu cầu này.";
                _context.Notifications.Remove(notification);
                await _context.SaveChangesAsync();
             

                // Kiểm tra an toàn và chuyển hướng về lại trang đó
                if (Url.IsLocalUrl(returnUrl))
                {
                    return Redirect(returnUrl);
                }
                return RedirectToAction("LoanDashboard");
            }
            await LogActivity("Từ chối gia hạn sách", $"Độc giả: {loan.Username}, Sách: '{loan.Book.Title}'");

            // SỬA LỖI 3: (Tùy chọn) Không nên reset cờ WasExtended.
            // Nếu bạn muốn người dùng có thể yêu cầu lại, hãy giữ dòng này.
            // Nếu không, hãy xóa hoặc chú thích nó đi.
            // loan.WasExtended = false;

            // Tạo thông báo từ chối cho người dùng
            var userNotification = new Notification
            {
                Username = loan.Username,
                Message = $"Yêu cầu gia hạn sách '{loan.Book.Title}' của bạn đã bị từ chối.",
                CreatedAt = DateTime.Now,
                LoanId = loan.LoanId
            };
            await _context.Notifications.AddAsync(userNotification);

            // Gửi email từ chối
            if (loan.TaiKhoan?.Email != null)
            {
                await _emailService.SendEmailAsync(
                    loan.TaiKhoan.Email,
                    "Yêu cầu gia hạn sách đã bị từ chối",
                    userNotification.Message
                );
            }

            // SỬA LỖI 2: Chỉ xóa thông báo gốc sau khi đã xử lý xong
            _context.Notifications.Remove(notification);
            await _context.SaveChangesAsync();

            // SỬA LỖI 2: Chỉ đặt thông báo thành công khi mọi thứ thực sự thành công
            TempData["SuccessMessage"] = "Đã từ chối yêu cầu gia hạn thành công.";
            return RedirectToAction("LoanDashboard");
        }
        // Trong file: LibrarianController.cs

        [HttpGet]
        [Route("api/librarian/notification-counts")] // Tạo một đường dẫn API riêng
        public async Task<IActionResult> GetNotificationCounts()
        {
            // Đếm số yêu cầu mượn sách đang chờ
            var pendingLoanCount = await _context.BookLoans
                .CountAsync(l => l.Status == "Pending");

            // Đếm số yêu cầu gia hạn đang chờ
            var pendingExtensionCount = await _context.Notifications
                .CountAsync(n => n.Message.StartsWith("YEUCAUGIAHANSACHDOCGIA:") && !n.IsRead);

            // Đếm số yêu cầu đặt trước đang chờ
            var pendingReservationCount = await _context.BookReservations
                .CountAsync(r => r.Status == "Pending");

            // Tạo một đối tượng để trả về kết quả dưới dạng JSON
            var counts = new
            {
                // Tổng số yêu cầu liên quan đến mượn/trả
                LoanManagement = pendingLoanCount + pendingExtensionCount,
                ReservationManagement = pendingReservationCount
            };

            return Json(counts);
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
        #endregion

    }
}