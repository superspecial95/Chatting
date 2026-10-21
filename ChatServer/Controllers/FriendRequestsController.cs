using System.Security.Claims;
using ChatServer.Data;
using ChatServer.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.SignalR;
using ChatServer.Hubs;

namespace ChatServer.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class FriendRequestsController : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly IHubContext<ChatHub> _hubContext;

    public FriendRequestsController(
        AppDbContext context,
        IHubContext<ChatHub> hubContext)
    {
        _context = context;
        _hubContext = hubContext;
    }

    // =========================
    // SEND FRIEND REQUEST
    // =========================

    [HttpPost("send")]
    public async Task<IActionResult> SendRequest(
        SendFriendRequest requestData)
    {
        string userIdString =
            User.FindFirstValue(
                ClaimTypes.NameIdentifier) ?? "";

        if (!int.TryParse(
                userIdString,
                out int senderId))
        {
            return Unauthorized();
        }

        string username =
            requestData.Username.Trim();

        if (string.IsNullOrWhiteSpace(username))
        {
            return BadRequest(
                "Username không được để trống.");
        }

        var receiver =
            await _context.Users
                .FirstOrDefaultAsync(
                    u => u.Username == username);

        if (receiver == null)
        {
            return NotFound(
                "Không tìm thấy username này.");
        }

        int receiverId = receiver.Id;

        if (senderId == receiverId)
        {
            return BadRequest(
                "Không thể gửi lời mời cho chính mình.");
        }

        // Kiểm tra lời mời đã gửi
        bool alreadyPending =
            await _context.FriendRequests.AnyAsync(
                f =>
                    f.SenderId == senderId &&
                    f.ReceiverId == receiverId &&
                    f.Status == "Pending");

        if (alreadyPending)
        {
            return BadRequest(
                "Bạn đã gửi lời mời cho người này.");
        }

        // Kiểm tra người kia đã gửi cho mình
        bool reversePending =
            await _context.FriendRequests.AnyAsync(
                f =>
                    f.SenderId == receiverId &&
                    f.ReceiverId == senderId &&
                    f.Status == "Pending");

        if (reversePending)
        {
            return BadRequest(
                "Người này đã gửi lời mời cho bạn.");
        }

        // Kiểm tra đã là bạn chưa
        bool alreadyFriends =
            await _context.FriendRequests.AnyAsync(
                f =>
                    f.Status == "Accepted" &&
                    (
                        (f.SenderId == senderId &&
                         f.ReceiverId == receiverId)
                        ||
                        (f.SenderId == receiverId &&
                         f.ReceiverId == senderId)
                    ));

        if (alreadyFriends)
        {
            return BadRequest(
                "Hai người đã là bạn bè.");
        }

        var friendRequest =
            new FriendRequest
            {
                SenderId = senderId,
                ReceiverId = receiverId,
                Status = "Pending",
                CreatedAt = DateTime.UtcNow
            };

        _context.FriendRequests.Add(
            friendRequest);

        await _context.SaveChangesAsync();

        string senderUsername =
            User.FindFirstValue(
                ClaimTypes.Name) ?? "Unknown";

        // Thông báo realtime cho người nhận
        await _hubContext.Clients.User(
            receiverId.ToString())
            .SendAsync(
                "FriendRequestReceived",
                senderId,
                senderUsername);

        return Ok(new
        {
            message = "Đã gửi lời mời kết bạn."
        });
    }


    // =========================
    // GET RECEIVED REQUESTS
    // =========================

    [HttpGet("received")]
    public async Task<IActionResult> GetReceivedRequests()
    {
        string userIdString =
            User.FindFirstValue(
                ClaimTypes.NameIdentifier) ?? "";

        if (!int.TryParse(
                userIdString,
                out int userId))
        {
            return Unauthorized();
        }

        var requests =
            await _context.FriendRequests
                .Where(f =>
                    f.ReceiverId == userId &&
                    f.Status == "Pending")
                .Join(
                    _context.Users,
                    request => request.SenderId,
                    user => user.Id,
                    (request, user) => new
                    {
                        requestId = request.Id,
                        userId = user.Id,
                        username = user.Username,
                        createdAt = request.CreatedAt
                    })
                .OrderByDescending(
                    x => x.createdAt)
                .ToListAsync();

        return Ok(requests);
    }


    // =========================
    // ACCEPT FRIEND
    // =========================

    [HttpPost("accept/{requestId}")]
    public async Task<IActionResult> AcceptRequest(
        int requestId)
    {
        string userIdString =
            User.FindFirstValue(
                ClaimTypes.NameIdentifier) ?? "";

        if (!int.TryParse(
                userIdString,
                out int userId))
        {
            return Unauthorized();
        }

        var request =
            await _context.FriendRequests
                .FirstOrDefaultAsync(
                    f =>
                        f.Id == requestId &&
                        f.ReceiverId == userId &&
                        f.Status == "Pending");

        if (request == null)
        {
            return NotFound(
                "Lời mời kết bạn không tồn tại.");
        }

        request.Status = "Accepted";

        await _context.SaveChangesAsync();

        // Người vừa chấp nhận
        var receiver =
            await _context.Users
                .FindAsync(userId);

        if (receiver != null)
        {
            // Báo realtime cho người đã gửi lời mời
            await _hubContext.Clients.User(
                request.SenderId.ToString())
                .SendAsync(
                    "FriendRequestAccepted",
                    userId,
                    receiver.Username);
        }

        return Ok(new
        {
            message =
                "Đã chấp nhận lời mời kết bạn.",

            friendId =
                request.SenderId
        });
    }


    // =========================
    // REJECT FRIEND
    // =========================

    [HttpPost("reject/{requestId}")]
    public async Task<IActionResult> RejectRequest(
        int requestId)
    {
        string userIdString =
            User.FindFirstValue(
                ClaimTypes.NameIdentifier) ?? "";

        if (!int.TryParse(
                userIdString,
                out int userId))
        {
            return Unauthorized();
        }

        var request =
            await _context.FriendRequests
                .FirstOrDefaultAsync(
                    f =>
                        f.Id == requestId &&
                        f.ReceiverId == userId &&
                        f.Status == "Pending");

        if (request == null)
        {
            return NotFound(
                "Lời mời kết bạn không tồn tại.");
        }

        request.Status = "Rejected";

        await _context.SaveChangesAsync();

        return Ok(new
        {
            message =
                "Đã từ chối lời mời kết bạn."
        });
    }
}


// =========================
// SEND FRIEND REQUEST MODEL
// =========================

public class SendFriendRequest
{
    [System.ComponentModel.DataAnnotations.Required]
    [System.ComponentModel.DataAnnotations.StringLength(20, MinimumLength = 3)]
    [System.ComponentModel.DataAnnotations.RegularExpression("^[a-zA-Z0-9_]+$")]
    public string Username { get; set; } = "";
}
