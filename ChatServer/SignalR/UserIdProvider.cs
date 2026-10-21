using System.Security.Claims;
using Microsoft.AspNetCore.SignalR;

namespace ChatServer.SignalR;

public class UserIdProvider : IUserIdProvider
{
    public string? GetUserId(
        HubConnectionContext connection)
    {
        return connection.User?
            .FindFirst(ClaimTypes.NameIdentifier)?
            .Value;
    }
}