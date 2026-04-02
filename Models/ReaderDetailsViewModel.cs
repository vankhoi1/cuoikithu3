// File: QuanLyThuVien.Models/ReaderDetailsViewModel.cs

using System;
using System.Collections.Generic;

namespace QuanLyThuVien.Models
{
    public class ReaderDetailsViewModel
    {
        // Thông tin cơ bản của độc giả (từ DocGia và TaiKhoan)
        public string Username { get; set; }
        public string HoTen { get; set; }
        public string Email { get; set; }
        public string SoDienThoai { get; set; }
        public DateTime? NgaySinh { get; set; }
        public string DiaChi { get; set; }
        public bool IsActive { get; set; } // Thêm trạng thái tài khoản

        // Danh sách các sách đang được mượn
        public List<BookLoan> CurrentlyBorrowedBooks { get; set; }

        // Danh sách các yêu cầu đặt trước đang chờ duyệt
        public List<BookReservation> PendingReservations { get; set; }

        // THÊM MỚI: Danh sách các yêu cầu mượn sách đang chờ duyệt
        public List<BookLoan> PendingLoanRequests { get; set; }

        // THÊM MỚI: Danh sách các yêu cầu gia hạn sách đang chờ duyệt (từ Notifications)
        // Chúng ta sẽ lấy Notification gốc để dễ dàng truyền NotificationId vào Action
        public List<Notification> PendingExtensionNotifications { get; set; }
        public List<ExtensionRequestViewModel> PendingExtensionRequests { get; set; }
    }
}