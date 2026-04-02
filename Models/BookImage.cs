using System.ComponentModel.DataAnnotations;
using QuanLyThuVien.Models;

public class BookImage
{
    public int Id { get; set; }

    public int BookId { get; set; }
    public string ImageUrl { get; set; } = string.Empty;
    public string ImagePath { get; set; } = string.Empty;
    public byte[]? ImageVector { get; set; }
    // Navigation property
    public Book Book { get; set; }
}
