using ChatServer.Data;
using ChatServer.Models;
using ChatServer.Services;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace ChatServer.Tests;

public class FriendshipServiceTests
{
    [Fact]
    public async Task AreFriendsAsync_returns_true_for_an_accepted_request_in_either_direction()
    {
        await using AppDbContext context = CreateContext();
        context.FriendRequests.Add(new FriendRequest
        {
            SenderId = 2,
            ReceiverId = 1,
            Status = "Accepted"
        });
        await context.SaveChangesAsync();

        var service = new FriendshipService(context);

        Assert.True(await service.AreFriendsAsync(1, 2));
        Assert.True(await service.AreFriendsAsync(2, 1));
    }

    [Fact]
    public async Task AreFriendsAsync_returns_false_for_pending_requests_or_the_same_user()
    {
        await using AppDbContext context = CreateContext();
        context.FriendRequests.Add(new FriendRequest
        {
            SenderId = 1,
            ReceiverId = 2,
            Status = "Pending"
        });
        await context.SaveChangesAsync();

        var service = new FriendshipService(context);

        Assert.False(await service.AreFriendsAsync(1, 2));
        Assert.False(await service.AreFriendsAsync(1, 1));
    }

    private static AppDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new AppDbContext(options);
    }
}
