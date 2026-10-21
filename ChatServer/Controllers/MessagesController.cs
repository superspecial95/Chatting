using System.Security.Claims;
using ChatServer.Data;
using ChatServer.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ChatServer.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class MessagesController : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly FriendshipService _friendshipService;

    public MessagesController(
        AppDbContext context,
        FriendshipService friendshipService)
    {
        _context = context;
        _friendshipService = friendshipService;
    }

    // =========================================================
    // LOAD CHAT HISTORY
    // =========================================================

    [HttpGet("{otherUserId}")]
    public async Task<IActionResult> GetMessages(
        int otherUserId)
    {
        string userIdString =
            User.FindFirstValue(
                ClaimTypes.NameIdentifier) ?? "";

        if (!int.TryParse(
                userIdString,
                out int currentUserId))
        {
            return Unauthorized();
        }

        if (!await _friendshipService.AreFriendsAsync(currentUserId, otherUserId))
        {
            return Forbid();
        }

        var messages =
            await _context.Messages
                .Where(
                    m =>
                        (m.SenderId == currentUserId &&
                         m.ReceiverId == otherUserId)
                        ||
                        (m.SenderId == otherUserId &&
                         m.ReceiverId == currentUserId))
                .OrderByDescending(m => m.SentAt)
                .Take(50)
                .OrderBy(m => m.SentAt)
                .Select(
                    m => new
                    {
                        senderId = m.SenderId,

                        username =
                            _context.Users
                                .Where(
                                    u =>
                                        u.Id ==
                                        m.SenderId)
                                .Select(
                                    u =>
                                        u.Username)
                                .FirstOrDefault(),

                        message =
                            m.Content,

                        sentAt =
                            m.SentAt,

                        fileName =
                            m.FileName,

                        fileUrl =
                            m.FileUrl,

                        isRead =
                            m.IsRead
                    })
                .ToListAsync();

        return Ok(messages);
    }

    // =========================================================
    // GET UNREAD COUNTS
    // =========================================================

    [HttpGet("unread-counts")]
    public async Task<IActionResult> GetUnreadCounts()
    {
        string userIdString =
            User.FindFirstValue(
                ClaimTypes.NameIdentifier) ?? "";

        if (!int.TryParse(
                userIdString,
                out int currentUserId))
        {
            return Unauthorized();
        }

        var unreadCounts =
            await _context.Messages
                .Where(
                    m =>
                        m.ReceiverId ==
                        currentUserId &&
                        !m.IsRead)
                .GroupBy(
                    m =>
                        m.SenderId)
                .Select(
                    g => new
                    {
                        userId =
                            g.Key,

                        count =
                            g.Count()
                    })
                .ToListAsync();

        return Ok(unreadCounts);
    }

    // =========================================================
    // MARK MESSAGES AS READ
    // =========================================================

    [HttpPost("mark-read/{otherUserId}")]
    public async Task<IActionResult> MarkMessagesAsRead(
        int otherUserId)
    {
        string userIdString =
            User.FindFirstValue(
                ClaimTypes.NameIdentifier) ?? "";

        if (!int.TryParse(
                userIdString,
                out int currentUserId))
        {
            return Unauthorized();
        }

        if (!await _friendshipService.AreFriendsAsync(currentUserId, otherUserId))
        {
            return Forbid();
        }

        var unreadMessages =
            await _context.Messages
                .Where(
                    m =>
                        m.SenderId ==
                        otherUserId &&
                        m.ReceiverId ==
                        currentUserId &&
                        !m.IsRead)
                .ToListAsync();

        foreach (var message in unreadMessages)
        {
            message.IsRead = true;
        }

        await _context.SaveChangesAsync();

        return Ok(
            new
            {
                message =
                    "Đã đánh dấu tin nhắn là đã đọc."
            });
    }
}
