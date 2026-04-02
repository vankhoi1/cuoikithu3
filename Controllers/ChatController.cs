using Microsoft.AspNetCore.Mvc;
using QuanLyThuVien.Data;
using QuanLyThuVien.Models;
using System;
using System.Linq;

namespace QuanLyThuVien.Controllers
{
    public class ChatController : Controller
    {
        private readonly LibraryDbContext _context;

        public ChatController(LibraryDbContext context)
        {
            _context = context;
        }

        // Trang chat cho độc giả
        public IActionResult ReaderChat()
        {
            return View("~/Views/Reader/Chat.cshtml");
        }

        // Trang dashboard chat của thủ thư
        public IActionResult ChatDashboard()
        {
            var readers = _context.ChatMessages
                .Select(m => m.FromUser)
                .Distinct()
                .ToList();

            ViewBag.Readers = readers;
            return View("~/Views/Librarian/ChatDashboard.cshtml");
        }

        // Lấy tin nhắn giữa 2 người
        [HttpGet]
        public IActionResult GetMessages(string reader)
        {
            var currentUser = User.Identity?.Name;

            // Nếu là độc giả => load chat giữa user và thủ thư
            if (!User.IsInRole("ThuThu") && !string.IsNullOrEmpty(currentUser))
            {
                reader = currentUser;
            }

            if (string.IsNullOrEmpty(reader))
                return BadRequest("Thiếu tên độc giả.");

            var messages = _context.ChatMessages
                .Where(m =>
                    (m.FromUser == reader && m.ToUser == "thu-thu") ||
                    (m.FromUser == "thu-thu" && m.ToUser == reader))
                .OrderBy(m => m.SentAt)
                .Select(m => new
                {
                    fromUser = m.FromUser,
                    message = m.Message,
                    sentAt = m.SentAt
                })
                .ToList();

            return Json(messages);
        }

        // Gửi tin nhắn (độc giả hoặc thủ thư đều dùng được)
        [HttpPost]
        public IActionResult SendMessage(string toUser, string message)
        {
            var fromUser = User.Identity?.Name ?? "khach";
            if (string.IsNullOrEmpty(toUser) || string.IsNullOrEmpty(message))
                return BadRequest("Thiếu thông tin.");

            var chatMessage = new ChatMessage
            {
                FromUser = fromUser,
                ToUser = toUser,
                Message = message,
                SentAt = DateTime.Now
            };

            _context.ChatMessages.Add(chatMessage);
            _context.SaveChanges();

            return Ok(new { success = true });
        }
        [HttpPost]
        public async Task<IActionResult> UploadImage(IFormFile image)
        {
            if (image == null || image.Length == 0)
                return BadRequest("No file uploaded");

            var uploads = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "chat-images");
            Directory.CreateDirectory(uploads);

            var fileName = Guid.NewGuid().ToString() + Path.GetExtension(image.FileName);
            var filePath = Path.Combine(uploads, fileName);

            using (var stream = new FileStream(filePath, FileMode.Create))
            {
                await image.CopyToAsync(stream);
            }

            var fileUrl = "/chat-images/" + fileName;
            return Json(new { url = fileUrl });
        }


    }
}
