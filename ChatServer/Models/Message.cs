namespace ChatServer.Models;

public class Message
{
    public int Id { get; set; }

    public int SenderId { get; set; }

    public int ReceiverId { get; set; }

    [System.ComponentModel.DataAnnotations.MaxLength(4_000)]
    public string Content { get; set; } = "";

    public DateTime SentAt { get; set; } = DateTime.UtcNow;

    [System.ComponentModel.DataAnnotations.MaxLength(255)]
    public string? FileName { get; set; }

    [System.ComponentModel.DataAnnotations.MaxLength(128)]
    public string? FileUrl { get; set; }

    public bool IsRead { get; set; } = false;
}
