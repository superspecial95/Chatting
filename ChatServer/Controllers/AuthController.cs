using ChatServer.Data;
using ChatServer.Models;
using ChatServer.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ChatServer.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly JwtService _jwtService;

    public AuthController(
        AppDbContext context,
        JwtService jwtService)
    {
        _context = context;
        _jwtService = jwtService;
    }

    [HttpPost("register")]
    public async Task<IActionResult> Register(RegisterRequest request)
    {
        string username = request.Username.Trim();
        string email = request.Email.Trim().ToLowerInvariant();

        if (await _context.Users.AnyAsync(u => u.Username == username))
        {
            return Conflict(new { message = "Username đã tồn tại." });
        }

        if (await _context.Users.AnyAsync(u => u.Email == email))
        {
            return Conflict(new { message = "Email đã tồn tại." });
        }

        var user = new User
        {
            Username = username,
            Email = email,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password)
        };

        _context.Users.Add(user);

        try
        {
            await _context.SaveChangesAsync();
        }
        catch (DbUpdateException)
        {
            return Conflict(new { message = "Username hoặc email đã tồn tại." });
        }

        return Ok(new
        {
            message = "Đăng ký thành công!",
            userId = user.Id,
            username = user.Username
        });
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login(LoginRequest request)
    {
        string username = request.Username.Trim();

        var user = await _context.Users
            .FirstOrDefaultAsync(u => u.Username == username);

        if (user == null)
        {
            return Unauthorized("Username hoặc password không đúng.");
        }

        bool passwordCorrect = BCrypt.Net.BCrypt.Verify(
            request.Password,
            user.PasswordHash);

        if (!passwordCorrect)
        {
            return Unauthorized("Username hoặc password không đúng.");
        }

        // Tạo JWT Token
        string token = _jwtService.GenerateToken(user);

        // Trả Token về Client
        return Ok(new
        {
            message = "Đăng nhập thành công!",
            userId = user.Id,
            username = user.Username,
            token = token
        });
    }
}

public class RegisterRequest
{
    [System.ComponentModel.DataAnnotations.Required]
    [System.ComponentModel.DataAnnotations.StringLength(20, MinimumLength = 3)]
    [System.ComponentModel.DataAnnotations.RegularExpression("^[a-zA-Z0-9_]+$")]
    public string Username { get; set; } = "";

    [System.ComponentModel.DataAnnotations.Required]
    [System.ComponentModel.DataAnnotations.EmailAddress]
    [System.ComponentModel.DataAnnotations.StringLength(254)]
    public string Email { get; set; } = "";

    [System.ComponentModel.DataAnnotations.Required]
    [System.ComponentModel.DataAnnotations.StringLength(100, MinimumLength = 8)]
    public string Password { get; set; } = "";
}
