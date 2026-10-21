using System.Security.Claims;
using ChatServer.Data;
using ChatServer.Models;
using ChatServer.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace ChatServer.Hubs;

[Authorize]
public class ChatHub : Hub
{
    private readonly AppDbContext _context;
    private readonly FriendshipService _friendshipService;
    private readonly OnlinePresenceTracker _presenceTracker;

    public ChatHub(
        AppDbContext context,
        FriendshipService friendshipService,
        OnlinePresenceTracker presenceTracker)
    {
        _context = context;
        _friendshipService = friendshipService;
        _presenceTracker = presenceTracker;
    }

    public override async Task OnConnectedAsync()
    {
        string userIdString =
            Context.User?.FindFirstValue(
                ClaimTypes.NameIdentifier) ?? "";

        if (int.TryParse(
                userIdString,
                out int userId))
        {
            var user =
                await _context.Users.FindAsync(userId);

            if (user != null)
            {
                if (_presenceTracker.AddConnection(userId))
                {
                    user.IsOnline = true;
                    await _context.SaveChangesAsync();

                    await Clients.All.SendAsync(
                        "UserStatusChanged",
                        userId,
                        true);
                }
            }
        }

        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(
        Exception? exception)
    {
        string userIdString =
            Context.User?.FindFirstValue(
                ClaimTypes.NameIdentifier) ?? "";

        if (int.TryParse(
                userIdString,
                out int userId))
        {
            var user =
                await _context.Users.FindAsync(userId);

            if (user != null)
            {
                if (_presenceTracker.RemoveConnection(userId))
                {
                    user.IsOnline = false;
                    await _context.SaveChangesAsync();

                    await Clients.All.SendAsync(
                        "UserStatusChanged",
                        userId,
                        false);
                }
            }
        }

        await base.OnDisconnectedAsync(exception);
    }

    // =========================================================
    // SEND MESSAGE
    // =========================================================

    public async Task SendMessage(
        int receiverId,
        string message)
    {
        if (string.IsNullOrWhiteSpace(message) || message.Length > 4_000)
        {
            throw new HubException("Tin nhắn phải chứa từ 1 đến 4.000 ký tự.");
        }

        string userIdString =
            Context.User?.FindFirstValue(
                ClaimTypes.NameIdentifier) ?? "";

        if (!int.TryParse(userIdString, out int senderId))
        {
            throw new HubException("Người dùng không hợp lệ.");
        }

        string username =
            Context.User?.FindFirstValue(
                ClaimTypes.Name) ?? "Unknown";

        var receiver =
            await _context.Users.FindAsync(
                receiverId);

        if (receiver == null)
        {
            throw new HubException("Không tìm thấy người nhận.");
        }

        if (!await _friendshipService.AreFriendsAsync(senderId, receiverId))
        {
            throw new HubException("Bạn chỉ có thể nhắn tin cho bạn bè đã chấp nhận.");
        }

        var newMessage =
            new Message
            {
                SenderId = senderId,

                ReceiverId = receiverId,

                Content = message,

                SentAt = DateTime.UtcNow,

                IsRead = false
            };

        _context.Messages.Add(
            newMessage);

        await _context.SaveChangesAsync();

        // Hiển thị cho chính người gửi
        await Clients.Caller.SendAsync(
            "ReceiveMessage",
            senderId,
            username,
            message);

        // Gửi cho người nhận nếu đang online
        await Clients.User(
            receiverId.ToString())
            .SendAsync(
                "ReceiveMessage",
                senderId,
                username,
                message);
    }

    // =========================================================
    // SEND FILE
    // =========================================================

    public async Task SendFile(
        int receiverId,
        Guid fileId)
    {
        string userIdString =
            Context.User?.FindFirstValue(
                ClaimTypes.NameIdentifier) ?? "";

        if (!int.TryParse(userIdString, out int senderId))
        {
            throw new HubException("Người dùng không hợp lệ.");
        }

        string username =
            Context.User?.FindFirstValue(
                ClaimTypes.Name) ?? "Unknown";

        var receiver =
            await _context.Users.FindAsync(
                receiverId);

        if (receiver == null)
        {
            throw new HubException("Không tìm thấy người nhận.");
        }

        if (!await _friendshipService.AreFriendsAsync(senderId, receiverId))
        {
            throw new HubException("Bạn chỉ có thể gửi tệp cho bạn bè đã chấp nhận.");
        }

        var uploadedFile = await _context.UploadedFiles.FindAsync(fileId);

        if (uploadedFile == null || uploadedFile.OwnerId != senderId)
        {
            throw new HubException("Tệp không tồn tại hoặc không thuộc về bạn.");
        }

        string fileUrl = $"/api/Files/{uploadedFile.Id}";

        var newMessage =
            new Message
            {
                SenderId = senderId,

                ReceiverId = receiverId,

                Content = "",

                FileName = uploadedFile.OriginalFileName,

                FileUrl = fileUrl,

                SentAt = DateTime.UtcNow,

                IsRead = false
            };

        _context.Messages.Add(
            newMessage);

        await _context.SaveChangesAsync();

        // Hiển thị cho chính người gửi
        await Clients.Caller.SendAsync(
            "ReceiveFile",
            senderId,
            username,
            uploadedFile.OriginalFileName,
            fileUrl);

        // Gửi cho người nhận nếu đang online
        await Clients.User(
            receiverId.ToString())
            .SendAsync(
                "ReceiveFile",
                senderId,
                username,
                uploadedFile.OriginalFileName,
                fileUrl);
    }
}
