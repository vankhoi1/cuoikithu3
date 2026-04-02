using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using QuanLyThuVien.Data;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using QuanLyThuVien.Models;

namespace QuanLyThuVien.Services
{
    public class OverdueCheckService : IHostedService, IDisposable
    {
        private readonly ILogger<OverdueCheckService> _logger;
        private Timer _timer;
        private readonly IServiceProvider _services;

        public OverdueCheckService(IServiceProvider services, ILogger<OverdueCheckService> logger)
        {
            _services = services;
            _logger = logger;
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Dịch vụ kiểm tra quá hạn đang khởi động.");
            // Timer sẽ gọi phương thức DoWork
            _timer = new Timer(DoWork, null, TimeSpan.FromSeconds(15), TimeSpan.FromMinutes(1)); 
            return Task.CompletedTask;
        }

        // Phương thức này có chữ ký "void" để tương thích với Timer
        // Nó đóng vai trò là một vỏ bọc an toàn để gọi logic chính
        private async void DoWork(object state)
        {
            try
            {
                await DoWorkAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Đã xảy ra lỗi không mong muốn trong dịch vụ kiểm tra quá hạn.");
            }
        }

        // Phương thức này trả về "Task" và chứa toàn bộ logic xử lý chính
        private async Task DoWorkAsync()
        {
            _logger.LogInformation("Dịch vụ kiểm tra quá hạn đang chạy quét...");
            using (var scope = _services.CreateScope())
            {
                var context = scope.ServiceProvider.GetRequiredService<LibraryDbContext>();
                var emailService = scope.ServiceProvider.GetRequiredService<GmailEmailService>();

                var overdueLoans = await context.BookLoans
                    .Include(l => l.TaiKhoan)
                    .Include(l => l.Book)
                    .Where(l => l.Status == "Approved"
                                 && l.ReturnDate == null
                                 && l.DueDate < DateTime.Now)
                    .ToListAsync();

                if (overdueLoans.Any())
                {
                    var recentOverdueNotifications = await context.Notifications
                        .Where(n => n.CreatedAt > DateTime.Now.AddHours(-23) && n.Message.Contains("quá hạn"))
                        .ToListAsync();

                    var recentlyNotified = new HashSet<string>(recentOverdueNotifications
                        .Select(n => $"{n.Username}_{n.LoanId}"));

                    var notificationsToSend = new List<Notification>();
                    foreach (var loan in overdueLoans)
                    {
                        bool hasBeenNotifiedRecently = recentlyNotified.Contains($"{loan.Username}_{loan.LoanId}");

                        if (!hasBeenNotifiedRecently)
                        {
                            _logger.LogInformation($"Phát hiện phiếu mượn #{loan.LoanId} của {loan.Username} quá hạn.");
                            var subject = "[Thông báo] Sách mượn đã quá hạn trả";
                            var body = $"Chào {loan.Username},<br><br>Hệ thống thư viện xin thông báo, cuốn sách '<b>{loan.Book.Title}</b>' bạn đang mượn đã quá hạn trả vào ngày {loan.DueDate:dd/MM/yyyy}.<br>Vui lòng mang sách đến thư viện để hoàn tất thủ tục trả sách trong thời gian sớm nhất.<br><br>Trân trọng,<br>Thư Viện Sách";

                            var notification = new Notification
                            {
                                Username = loan.Username,
                                Message = $"Sách '{loan.Book.Title}' bạn đang mượn đã quá hạn trả ngày {loan.DueDate:dd/MM/yyyy}.",
                                CreatedAt = DateTime.Now,
                                IsRead = false,
                                LoanId = loan.LoanId
                            };
                            notificationsToSend.Add(notification);

                            await emailService.SendEmailAsync(loan.TaiKhoan.Email, subject, body);
                        }
                    }

                    if (notificationsToSend.Any())
                    {
                        await context.Notifications.AddRangeAsync(notificationsToSend);
                        await context.SaveChangesAsync();
                        _logger.LogInformation($"Đã tạo và gửi {notificationsToSend.Count} thông báo quá hạn mới.");
                    }
                }
                else
                {
                    _logger.LogInformation("Không có phiếu mượn quá hạn nào.");
                }
            }
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Dịch vụ kiểm tra quá hạn đang dừng.");
            _timer?.Change(Timeout.Infinite, 0);
            return Task.CompletedTask;
        }

        public void Dispose()
        {
            _timer?.Dispose();
        }
    }
}