namespace ChatServer.Models;

public class User
{
    public int Id { get; set; }

    [System.ComponentModel.DataAnnotations.MaxLength(20)]
    public string Username { get; set; } = "";

    [System.ComponentModel.DataAnnotations.MaxLength(254)]
    public string Email { get; set; } = "";

    public bool IsOnline { get; set; } = false;

    [System.ComponentModel.DataAnnotations.MaxLength(255)]
    public string PasswordHash { get; set; } = "";

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
