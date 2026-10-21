namespace ChatServer.Models;

public class LoginRequest
{
    [System.ComponentModel.DataAnnotations.Required]
    [System.ComponentModel.DataAnnotations.StringLength(20, MinimumLength = 3)]
    public string Username { get; set; } = "";

    [System.ComponentModel.DataAnnotations.Required]
    [System.ComponentModel.DataAnnotations.StringLength(100, MinimumLength = 1)]
    public string Password { get; set; } = "";
}
