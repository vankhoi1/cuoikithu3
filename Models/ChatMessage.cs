using System;
using System.ComponentModel.DataAnnotations;

namespace QuanLyThuVien.Models
{
    public class ChatMessage
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public string FromUser { get; set; }

        [Required]
        public string ToUser { get; set; }

        [Required]
        public string Message { get; set; }

        public DateTime SentAt { get; set; } = DateTime.Now;
    }
}
