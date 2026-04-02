using Microsoft.EntityFrameworkCore;
using QuanLyThuVien.Models;

namespace QuanLyThuVien.Data
{
    public class LibraryDbContext : DbContext
    {
        public LibraryDbContext(DbContextOptions<LibraryDbContext> options) : base(options)
        {
        }

        public DbSet<TaiKhoan> TaiKhoan { get; set; }
        public DbSet<XacThucEmail> XacThucEmail { get; set; }
        public DbSet<Book> Books { get; set; }
        public DbSet<BookImage> BookImages { get; set; }
        public DbSet<BookLoan> BookLoans { get; set; }
        public DbSet<BookReservation> BookReservations { get; set; }
        public DbSet<Genre> Genres { get; set; }
        public DbSet<DocGia> DocGia { get; set; }
        public DbSet<Notification> Notifications { get; set; }
        public DbSet<ChatMessage> ChatMessages { get; set; }
        public DbSet<LibrarianActivity> LibrarianActivities { get; set; }
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Cấu hình cho BookReservation
            modelBuilder.Entity<BookReservation>()
                .HasOne(r => r.Book)
                .WithMany(b => b.BookReservations)
                .HasForeignKey(r => r.BookId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<BookReservation>()
                .HasOne(r => r.TaiKhoan)
                .WithMany()
                .HasForeignKey(r => r.Username)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<BookReservation>()
                .Property(r => r.Status)
                .HasDefaultValue("Pending");

            // Cấu hình cho BookLoan
            modelBuilder.Entity<BookLoan>()
                .HasOne(l => l.Book)
                .WithMany(b => b.BookLoans)
                .HasForeignKey(l => l.BookId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<BookLoan>()
                .HasOne(l => l.TaiKhoan)
                .WithMany()
                .HasForeignKey(l => l.Username)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<BookLoan>()
                .Property(l => l.Status)
                .HasDefaultValue("Pending");
        }
    }
}