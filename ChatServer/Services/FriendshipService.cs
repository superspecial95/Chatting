using ChatServer.Data;
using Microsoft.EntityFrameworkCore;

namespace ChatServer.Services;

public sealed class FriendshipService
{
    private readonly AppDbContext _context;

    public FriendshipService(AppDbContext context)
    {
        _context = context;
    }

    public Task<bool> AreFriendsAsync(int firstUserId, int secondUserId)
    {
        if (firstUserId == secondUserId)
        {
            return Task.FromResult(false);
        }

        return _context.FriendRequests.AnyAsync(request =>
            request.Status == "Accepted" &&
            ((request.SenderId == firstUserId && request.ReceiverId == secondUserId) ||
             (request.SenderId == secondUserId && request.ReceiverId == firstUserId)));
    }
}
