using Microsoft.AspNetCore.SignalR;
using QuanLyThuVien.Data;
using QuanLyThuVien.Models;
using System.Threading.Tasks;
using System.Collections.Concurrent;
using System.Linq;
using System;

namespace QuanLyThuVien.Hubs
{
    public class ChatHub : Hub
    {
        private static readonly ConcurrentDictionary<string, string> _connections = new();
        private readonly LibraryDbContext _context;

        public ChatHub(LibraryDbContext context)
        {
            _context = context;
        }

        public async Task SendMessage(string fromUser, string toUser, string message)
        {
            try
            {
                Console.WriteLine($"📩 {fromUser} gửi cho {toUser}: {message}");

                if (string.IsNullOrWhiteSpace(message))
                    return;

                var chat = new ChatMessage
                {
                    FromUser = fromUser,
                    ToUser = toUser,
                    Message = message,
                    SentAt = DateTime.Now
                };

                _context.ChatMessages.Add(chat);
                await _context.SaveChangesAsync();

                if (_connections.TryGetValue(toUser, out var connectionId))
                    await Clients.Client(connectionId).SendAsync("ReceiveMessage", fromUser, message);

                await Clients.Caller.SendAsync("ReceiveMessage", fromUser, message);
            }
            catch (Exception ex)
            {
                Console.WriteLine("🔥 Lỗi khi gửi tin nhắn: " + ex.Message);
                throw; // để SignalR gửi lỗi lên JS
            }
        }


        public override async Task OnConnectedAsync()
        {
            string user = Context.GetHttpContext()?.Request.Query["user"];
            if (!string.IsNullOrEmpty(user))
                _connections[user] = Context.ConnectionId;

            await base.OnConnectedAsync();
        }

        public override async Task OnDisconnectedAsync(Exception? exception)
        {
            string? user = _connections.FirstOrDefault(x => x.Value == Context.ConnectionId).Key;
            if (user != null)
                _connections.TryRemove(user, out _);

            await base.OnDisconnectedAsync(exception);
        }

    }
}
