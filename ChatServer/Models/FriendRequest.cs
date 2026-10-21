namespace ChatServer.Models;

public class FriendRequest
{
    public int Id { get; set; }

    public int SenderId { get; set; }

    public int ReceiverId { get; set; }

    [System.ComponentModel.DataAnnotations.MaxLength(16)]
    public string Status { get; set; } = "Pending";

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
