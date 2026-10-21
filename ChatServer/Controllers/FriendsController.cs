using System.Security.Claims;
using ChatServer.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ChatServer.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class FriendsController : ControllerBase
{
    private readonly AppDbContext _context;

    public FriendsController(AppDbContext context)
    {
        _context = context;
    }

    // =========================
    // GET MY FRIENDS
    // =========================

    [HttpGet]
    public async Task<IActionResult> GetFriends()
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

        var friends =
            await _context.FriendRequests
                .Where(f =>
                    f.Status == "Accepted" &&
                    (f.SenderId == userId ||
                     f.ReceiverId == userId))
                .Select(f => new
                {
                    friendId =
                        f.SenderId == userId
                            ? f.ReceiverId
                            : f.SenderId
                })
                .Join(
                    _context.Users,
                    x => x.friendId,
                    user => user.Id,
                    (x, user) => new
                    {
                        id = user.Id,
                        username = user.Username,
                        isOnline = user.IsOnline
                    })
                .OrderBy(x => x.username)
                .ToListAsync();

        return Ok(friends);
    }
}