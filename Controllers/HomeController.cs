using Microsoft.AspNetCore.Mvc; // MVC
using QuanLyThuVien.Data; //QLTV
using Microsoft.EntityFrameworkCore;//Code
using System.Threading.Tasks;
using System.Linq;
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

        public async Task<IActionResult> Index(string searchString, string genre, string filter, List<int> resultIds)
        {
            // Tải danh sách thể loại cho dropdown 
            var allGenreStrings = await _context.Books.Where(b => !string.IsNullOrEmpty(b.Genre)).Select(b => b.Genre).ToListAsync();
            var individualGenres = allGenreStrings.SelectMany(g => g.Split(',')).Select(g => g.Trim()).Where(g => !string.IsNullOrEmpty(g)).Distinct().OrderBy(g => g);
            ViewBag.Genres = individualGenres.ToList();

            var booksQuery = _context.Books.Include(b => b.BookImages).AsQueryable();

            // THAY ĐỔI QUAN TRỌNG: Ưu tiên xử lý kết quả từ tìm kiếm ảnh
            if (resultIds != null && resultIds.Any())
            {
                booksQuery = booksQuery.Where(b => resultIds.Contains(b.BookId));
            }
            else // Nếu không phải kết quả từ tìm ảnh, thì xử lý tìm kiếm bằng chữ như cũ
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
                }
            }

            var bookList = await booksQuery.ToListAsync();

            // Code xử lý ViewModel giữ nguyên như cũ
            // Code xử lý ViewModel giữ nguyên như cũ
            var borrowedIds = new HashSet<int>();
            var reservedIds = new HashSet<int>();
            var pendingLoanIds = new HashSet<int>();
            if (User.Identity.IsAuthenticated)
            {
                var username = User.Identity.Name;
                borrowedIds = (await _context.BookLoans.Where(l => l.Username == username && l.Status == "Approved" && l.ReturnDate == null).Select(l => l.BookId).ToListAsync()).ToHashSet();
                reservedIds = (await _context.BookReservations.Where(r => r.Username == username && r.Status == "Pending").Select(r => r.BookId).ToListAsync()).ToHashSet();
                pendingLoanIds = (await _context.BookLoans.Where(l => l.Username == username && l.Status == "Pending").Select(l => l.BookId).ToListAsync()).ToHashSet();
            }

            var viewModel = new BookListViewModel
            {
                Books = bookList,
                BorrowedBookIds = borrowedIds,
                ReservedBookIds = reservedIds,
                PendingLoanBookIds = pendingLoanIds
            };

            ViewData["SearchString"] = searchString;
            ViewData["Genre"] = genre;
            ViewData["Filter"] = filter;

            return View(viewModel);
        }
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
                TempData["ErrorMessage"] = "Không phân tích được hình ảnh này.";
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

            // THAY ĐỔI QUAN TRỌNG: Chuyển hướng về action Index với danh sách ID tìm được
            return RedirectToAction("Index", new { resultIds = matchingBookIds });
        }
    }
    }