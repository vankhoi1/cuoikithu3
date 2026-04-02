using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QuanLyThuVien.Data;
using QuanLyThuVien.Models;
using QuanLyThuVien.Services;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace QuanLyThuVien.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class ApiController : ControllerBase
    {
        private readonly LibraryDbContext _context;
        private readonly OnnxImageService _onnxImageService;
        private readonly GmailEmailService _emailService;

        public ApiController(LibraryDbContext context, OnnxImageService onnxImageService, GmailEmailService emailService)
        {
            _context = context;
            _onnxImageService = onnxImageService;
            _emailService = emailService;
        }

        /// <summary>
        /// Tìm kiếm sách bằng hình ảnh (ONNX)
        /// </summary>
        /// <param name="imageFile">File hình ảnh cần tìm kiếm</param>
        /// <returns>Danh sách sách tìm thấy với độ tương đồng</returns>
        [HttpPost("search-by-image")]
        public async Task<IActionResult> SearchByImage([FromForm] IFormFile imageFile)
        {
            if (imageFile == null || imageFile.Length == 0)
            {
                return BadRequest(new { error = "Vui lòng chọn file hình ảnh" });
            }

            try
            {
                float[] queryVector;
                using (var memoryStream = new MemoryStream())
                {
                    await imageFile.CopyToAsync(memoryStream);
                    memoryStream.Position = 0;
                    queryVector = await _onnxImageService.GetImageVectorAsync(memoryStream);
                }

                if (queryVector == null)
                {
                    return BadRequest(new { error = "Không thể phân tích hình ảnh này" });
                }

                var allBookImages = await _context.BookImages
                    .Include(bi => bi.Book)
                    .Where(bi => bi.ImageVector != null)
                    .ToListAsync();

                var results = allBookImages
                    .Select(bi => new
                    {
                        BookId = bi.BookId,
                        BookTitle = bi.Book?.Title ?? "Không có tiêu đề",
                        Author = bi.Book?.Author ?? "Không xác định",
                        Genre = bi.Book?.Genre ?? "Không xác định",
                        CoverImage = bi.ImageUrl,
                        Similarity = VectorMath.CosineSimilarity(queryVector, VectorMath.ToFloatArray(bi.ImageVector!))
                    })
                    .Where(r => r.Similarity > 0.5)
                    .OrderByDescending(r => r.Similarity)
                    .GroupBy(r => r.BookId)
                    .Select(g => g.First())
                    .ToList();

                return Ok(new
                {
                    success = true,
                    count = results.Count,
                    results
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = $"Lỗi server: {ex.Message}" });
            }
        }

        /// <summary>
        /// Lấy danh sách tin nhắn giữa người dùng
        /// </summary>
        /// <param name="reader">Tên người dùng (để lấy tin nhắn)</param>
        /// <returns>Danh sách tin nhắn</returns>
        [HttpGet("chat/messages")]
        public IActionResult GetChatMessages([FromQuery] string reader)
        {
            var currentUser = User.Identity?.Name;

            // Nếu là độc giả => load chat giữa user và thủ thư
            if (!User.IsInRole("ThuThu") && !string.IsNullOrEmpty(currentUser))
            {
                reader = currentUser;
            }

            if (string.IsNullOrEmpty(reader))
                return BadRequest(new { error = "Thiếu tên độc giả" });

            var messages = _context.ChatMessages
                .Where(m =>
                    (m.FromUser == reader && m.ToUser == "thu-thu") ||
                    (m.FromUser == "thu-thu" && m.ToUser == reader))
                .OrderBy(m => m.SentAt)
                .Select(m => new
                {
                    fromUser = m.FromUser,
                    toUser = m.ToUser,
                    message = m.Message,
                    sentAt = m.SentAt
                })
                .ToList();

            return Ok(new { success = true, messages });
        }

        /// <summary>
        /// Gửi tin nhắn
        /// </summary>
        /// <param name="toUser">Người nhận</param>
        /// <param name="message">Nội dung tin nhắn</param>
        /// <returns>Kết quả gửi tin nhắn</returns>
        [HttpPost("chat/send")]
        public async Task<IActionResult> SendChatMessage([FromForm] string toUser, [FromForm] string message)
        {
            var fromUser = User.Identity?.Name ?? "khach";
            if (string.IsNullOrEmpty(toUser) || string.IsNullOrEmpty(message))
                return BadRequest(new { error = "Thiếu thông tin" });

            var chatMessage = new ChatMessage
            {
                FromUser = fromUser,
                ToUser = toUser,
                Message = message,
                SentAt = DateTime.Now
            };

            _context.ChatMessages.Add(chatMessage);
            await _context.SaveChangesAsync();

            return Ok(new { success = true, message = "Tin nhắn đã được gửi" });
        }

        /// <summary>
        /// Gửi email thông báo
        /// </summary>
        /// <param name="toEmail">Email người nhận</param>
        /// <param name="subject">Tiêu đề email</param>
        /// <param name="body">Nội dung email</param>
        /// <returns>Kết quả gửi email</returns>
        [HttpPost("email/send")]
        public async Task<IActionResult> SendEmailNotification([FromForm] string toEmail, [FromForm] string subject, [FromForm] string body)
        {
            if (string.IsNullOrEmpty(toEmail) || string.IsNullOrEmpty(subject) || string.IsNullOrEmpty(body))
                return BadRequest(new { error = "Thiếu thông tin email" });

            try
            {
                var result = await _emailService.SendEmailAsync(toEmail, subject, body);
                return Ok(new { success = result, message = result ? "Email đã được gửi" : "Gửi email thất bại" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = $"Lỗi khi gửi email: {ex.Message}" });
            }
        }

        /// <summary>
        /// Lấy danh sách phiếu mượn
        /// </summary>
        /// <param name="status">Trạng thái (Pending, Approved, Returned)</param>
        /// <returns>Danh sách phiếu mượn</returns>
        [HttpGet("loans")]
        public async Task<IActionResult> GetLoans([FromQuery] string? status = null)
        {
            var query = _context.BookLoans
                .Include(l => l.Book)
                .Include(l => l.TaiKhoan)
                .AsQueryable();

            if (!string.IsNullOrEmpty(status))
            {
                query = query.Where(l => l.Status == status);
            }

            var loans = await query
                .OrderByDescending(l => l.BorrowDate)
                .Select(l => new
                {
                    loanId = l.LoanId,
                    bookTitle = l.Book.Title,
                    username = l.Username,
                    userEmail = l.TaiKhoan.Email,
                    borrowDate = l.BorrowDate,
                    dueDate = l.DueDate,
                    returnDate = l.ReturnDate,
                    status = l.Status
                })
                .ToListAsync();

            return Ok(new { success = true, loans });
        }

        /// <summary>
        /// Duyệt phiếu mượn
        /// </summary>
        /// <param name="loanId">ID phiếu mượn</param>
        /// <returns>Kết quả duyệt</returns>
        [HttpPost("loans/{loanId}/approve")]
        public async Task<IActionResult> ApproveLoan(int loanId)
        {
            var loan = await _context.BookLoans
                .Include(l => l.Book)
                .Include(l => l.TaiKhoan)
                .FirstOrDefaultAsync(l => l.LoanId == loanId && l.Status == "Pending");

            if (loan == null)
                return NotFound(new { error = "Không tìm thấy phiếu mượn hoặc phiếu mượn không ở trạng thái chờ duyệt" });

            loan.Status = "Approved";
            loan.DueDate = DateTime.Now.AddDays(14);

            var notification = new Notification
            {
                Username = loan.Username,
                Message = $"Yêu cầu mượn sách '{loan.Book.Title}' của bạn đã được duyệt. Hạn trả: {loan.DueDate.Value.ToLocalTime():dd/MM/yyyy}.",
                CreatedAt = DateTime.Now
            };

            await _context.Notifications.AddAsync(notification);
            await _context.SaveChangesAsync();

            // Gửi email thông báo
            await _emailService.SendEmailAsync(loan.TaiKhoan.Email, "Yêu cầu mượn sách được duyệt", notification.Message);

            return Ok(new { success = true, message = "Đã duyệt phiếu mượn thành công" });
        }

        /// <summary>
        /// Xác nhận trả sách
        /// </summary>
        /// <param name="loanId">ID phiếu mượn</param>
        /// <returns>Kết quả trả sách</returns>
        [HttpPost("loans/{loanId}/return")]
        public async Task<IActionResult> ReturnBook(int loanId)
        {
            var loan = await _context.BookLoans
                .Include(l => l.Book)
                .Include(l => l.TaiKhoan)
                .FirstOrDefaultAsync(l => l.LoanId == loanId && l.Status == "Approved" && l.ReturnDate == null);

            if (loan == null)
                return NotFound(new { error = "Không tìm thấy phiếu mượn hoặc sách đã được trả" });

            loan.ReturnDate = DateTime.Now;
            loan.Status = "Returned";
            loan.Book.SoLuong++;

            var returnNotification = new Notification
            {
                Username = loan.Username,
                Message = $"Bạn đã trả sách '{loan.Book.Title}' thành công vào ngày {loan.ReturnDate.Value.ToLocalTime():dd/MM/yyyy}.",
                CreatedAt = DateTime.Now,
            };

            await _context.Notifications.AddAsync(returnNotification);
            await _context.SaveChangesAsync();

            // Gửi email thông báo
            await _emailService.SendEmailAsync(loan.TaiKhoan.Email, "Xác nhận trả sách", returnNotification.Message);

            return Ok(new { success = true, message = "Đã xác nhận trả sách thành công" });
        }
    }
}