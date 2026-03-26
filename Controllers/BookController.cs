using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QuanLyThuVien.Data;
using QuanLyThuVien.Models;
using System.Linq;
using System.Threading.Tasks;

namespace QuanLyThuVien.Controllers
{
    public class BookController : Controller
    {
        private readonly LibraryDbContext _context;

        public BookController(LibraryDbContext context)
        {
            _context = context;
        }

        public async Task<IActionResult> Details(int id)
        {
            var book = await _context.Books
                .Include(b => b.BookImages)
                .FirstOrDefaultAsync(b => b.BookId == id);

            if (book == null)
            {
                return NotFound();
            }

            return View(book);
        }
        public async Task<IActionResult> BookList(string searchString, string genre)
        {
            var booksQuery = _context.Books
                .Include(b => b.BookImages)
                .AsQueryable();

            if (!string.IsNullOrEmpty(searchString))
            {
                booksQuery = booksQuery.Where(b =>
                    b.Title.Contains(searchString) ||
                    b.Author.Contains(searchString));
            }

            if (!string.IsNullOrEmpty(genre))
            {
                booksQuery = booksQuery.Where(b => b.Genre == genre);
            }

            var books = await booksQuery.ToListAsync();
            return View(books);
        }

    }
}
