using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QuanLyThuVien.Data;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using System.Collections.Generic;
using QuanLyThuVien.Models; // Để sử dụng BookListViewModel
using System.Security.Claims;   // Để lấy thông tin người dùng

namespace QuanLyThuVien.Controllers
{
    public class HomeController : Controller
    {
        private readonly LibraryDbContext _context;
        private readonly ILogger<HomeController> _logger;
        private readonly OnnxImageService _onnxImageService;
        public HomeController(LibraryDbContext context, ILogger<HomeController> logger, OnnxImageService onnxImageService)
        {
            _context = context;
            _logger = logger;
            _onnxImageService = onnxImageService;
        }

        // Hãy dán đè code này lên trên phương thức Index() cũ của bạn

        // Trong file: HomeController.cs

        // Trong file: HomeController.cs

        // THAY THẾ TOÀN BỘ phương thức Index() cũ bằng code này:
        // Trong file: HomeController.cs

        public async Task<IActionResult> Index(string searchString, string genre, string filter, List<int> resultIds, bool imageSearch = false)
        {
            // Tải danh sách thể loại cho dropdown (bắt buộc)
            var allGenreStrings = await _context.Books.Where(b => !string.IsNullOrEmpty(b.Genre)).Select(b => b.Genre).ToListAsync();
            var individualGenres = allGenreStrings.SelectMany(g => g.Split(',')).Select(g => g.Trim()).Where(g => !string.IsNullOrEmpty(g)).Distinct().OrderBy(g => g);
            ViewBag.Genres = individualGenres.ToList();

            // 1. Lấy thông tin người dùng VÀ sách họ đang mượn/chờ LÊN ĐẦU
            var borrowedIds = new HashSet<int>();
            var reservedIds = new HashSet<int>();
            var pendingLoanIds = new HashSet<int>();
            var username = User.Identity.Name;

            if (username != null) // Kiểm tra username
            {
                borrowedIds = (await _context.BookLoans.Where(l => l.Username == username && l.Status == "Approved" && l.ReturnDate == null).Select(l => l.BookId).ToListAsync()).ToHashSet();
                reservedIds = (await _context.BookReservations.Where(r => r.Username == username && r.Status == "Pending").Select(r => r.BookId).ToListAsync()).ToHashSet();
                pendingLoanIds = (await _context.BookLoans.Where(l => l.Username == username && l.Status == "Pending").Select(l => l.BookId).ToListAsync()).ToHashSet();
            }

            var booksQuery = _context.Books.Include(b => b.BookImages).AsQueryable();

            // 2. SỬA LOGIC LỌC
            if (imageSearch)
            {
                // Nếu là kết quả từ TÌM ẢNH:
                // Luôn lọc theo resultIds, kể cả khi nó rỗng (để trả về 0 kết quả)
                booksQuery = booksQuery.Where(b => resultIds.Contains(b.BookId));
            }
            else // Nếu là TÌM BẰNG CHỮ (hoặc tải trang bình thường)
            {
                if (!string.IsNullOrEmpty(searchString))
                {
                    var lowerSearchString = searchString.ToLower();
                    booksQuery = booksQuery.Where(b => b.Title.ToLower().Contains(lowerSearchString) || b.Author.ToLower().Contains(lowerSearchString));
                }
                if (!string.IsNullOrEmpty(genre))
                {
                    booksQuery = booksQuery.Where(b => b.Genre.Contains(genre));
                }
                if (!string.IsNullOrEmpty(filter))
                {
                    if (filter == "available") booksQuery = booksQuery.Where(b => b.SoLuong > 0);
                    else if (filter == "borrowed") booksQuery = booksQuery.Where(b => b.SoLuong <= 0);

                    // Sửa lại logic "mybooks" để bao gồm cả sách đang chờ duyệt
                    else if (filter == "mybooks")
                    {
                        if (User.Identity.IsAuthenticated)
                        {
                            // Lọc theo danh sách ID đã lấy ở trên
                            booksQuery = booksQuery.Where(b => borrowedIds.Contains(b.BookId) || pendingLoanIds.Contains(b.BookId));
                        }
                        else
                        {
                            // Nếu chưa đăng nhập, trả về danh sách rỗng
                            booksQuery = booksQuery.Where(b => false);
                        }
                    }
                }
            }

            var bookList = await booksQuery.ToListAsync();

            var viewModel = new BookListViewModel
            {
                Books = bookList, // 'bookList' bây giờ sẽ rỗng nếu không tìm thấy
                BorrowedBookIds = borrowedIds,
                ReservedBookIds = reservedIds,
                PendingLoanBookIds = pendingLoanIds
            };

            ViewData["SearchString"] = searchString;
            ViewData["Genre"] = genre;
            ViewData["Filter"] = filter;

            return View(viewModel);
        }

// ... (Các phương thức khác trong HomeController.cs vẫn giữ nguyên)


// ... (Các phương thức khác như GetBooksApi, SearchByImage... giữ nguyên)

        // ... (Các phương thức khác trong HomeController.cs vẫn giữ nguyên)
        // [MỚI] Action API để xử lý tìm kiếm bằng AJAX
        [HttpGet]
        [Route("api/home/books")] // Endpoint API mới
        public async Task<IActionResult> GetBooksApi(string searchString, string genre, string filter)
        {
            var booksQuery = _context.Books.Include(b => b.BookImages).AsQueryable();

            var borrowedIds = new HashSet<int>();
            var reservedIds = new HashSet<int>();
            var pendingLoanIds = new HashSet<int>();

            if (User.Identity.IsAuthenticated)
            {
                var username = User.FindFirstValue(ClaimTypes.Name);

                borrowedIds = (await _context.BookLoans
                    .Where(l => l.Username == username && l.Status == "Approved" && l.ReturnDate == null)
                    .Select(l => l.BookId)
                    .ToListAsync()).ToHashSet();

                reservedIds = (await _context.BookReservations
                    .Where(r => r.Username == username && r.Status == "Pending")
                    .Select(r => r.BookId)
                    .ToListAsync()).ToHashSet();

                pendingLoanIds = (await _context.BookLoans
                    .Where(l => l.Username == username && l.Status == "Pending")
                    .Select(l => l.BookId)
                    .ToListAsync()).ToHashSet();
            }

            if (!string.IsNullOrEmpty(searchString))
            {
                var lowerSearchString = searchString.ToLower();
                booksQuery = booksQuery.Where(b => b.Title.ToLower().Contains(lowerSearchString) || b.Author.ToLower().Contains(lowerSearchString));
            }
            if (!string.IsNullOrEmpty(genre))
            {
                booksQuery = booksQuery.Where(b => b.Genre.Contains(genre));
            }
            if (!string.IsNullOrEmpty(filter))
            {
                if (filter == "available") booksQuery = booksQuery.Where(b => b.SoLuong > 0);
                else if (filter == "borrowed") booksQuery = booksQuery.Where(b => b.SoLuong <= 0);
                else if (filter == "mybooks" && User.Identity.IsAuthenticated) booksQuery = booksQuery.Where(b => borrowedIds.Contains(b.BookId) || pendingLoanIds.Contains(b.BookId)); // Lọc cả sách đang mượn và đang chờ duyệt
            }

            var bookList = await booksQuery
                .OrderByDescending(b => b.SoLuong > 0)
                .ThenBy(b => borrowedIds.Contains(b.BookId))
                .ToListAsync();

            var bookData = bookList.Select(b => new
            {
                b.BookId,
                b.Title,
                b.Author,
                b.Genre,
                b.PublicationYear,
                b.SoLuong,
                IsAvailable = b.SoLuong > 0,
                CoverImagePath = b.BookImages.FirstOrDefault()?.ImageUrl ?? "/images/no-image.jpg",
                IsBorrowed = borrowedIds.Contains(b.BookId),
                IsReserved = reservedIds.Contains(b.BookId),
                IsPending = pendingLoanIds.Contains(b.BookId)
            }).ToList();

            return Json(bookData);
        }
        // >>> THÊM PHƯƠNG THỨC MỚI NÀY VÀO CONTROLLER <<<
        [HttpPost]
        public async Task<IActionResult> SearchByImage(IFormFile imageFile)
        {
            if (imageFile == null || imageFile.Length == 0)
            {
                return RedirectToAction("Index");
            }

            float[] queryVector;
            using (var memoryStream = new MemoryStream())
            {
                await imageFile.CopyToAsync(memoryStream);
                memoryStream.Position = 0;
                queryVector = await _onnxImageService.GetImageVectorAsync(memoryStream);
            }

            if (queryVector == null)
            {
                TempData["ErrorMessage"] = "Không thể phân tích hình ảnh này.";
                return RedirectToAction("Index");
            }

            var allBookImages = await _context.BookImages
                .Where(bi => bi.ImageVector != null)
                .ToListAsync();

            var matchingBookIds = allBookImages
                .Select(bi => new {
                    BookId = bi.BookId,
                    Similarity = VectorMath.CosineSimilarity(queryVector, VectorMath.ToFloatArray(bi.ImageVector))
                })
                .Where(r => r.Similarity > 0.5)
                .OrderByDescending(r => r.Similarity)
                .Select(r => r.BookId)
                .Distinct()
                .ToList();

            TempData["SuccessMessage"] = $"Tìm thấy {matchingBookIds.Count} kết quả giống với hình ảnh của bạn.";
            // === THÊM 5 DÒNG NÀY VÀO ===
            if (matchingBookIds.Any())
            {
                TempData["SuccessMessage"] = $"Tìm thấy {matchingBookIds.Count} kết quả giống với hình ảnh của bạn.";
            }
            else
            {
                TempData["ErrorMessage"] = "Không tìm thấy sách nào giống với hình ảnh của bạn.";
            }
            // === KẾT THÚC THÊM MỚI ===
            // THAY ĐỔI QUAN TRỌNG: Chuyển hướng về action Index với danh sách ID tìm được
            // ... (code tìm kiếm) ...
            return RedirectToAction("Index", new { resultIds = matchingBookIds, imageSearch = true });
        }
    }
    }