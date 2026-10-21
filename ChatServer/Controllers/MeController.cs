using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ChatServer.Controllers;

[ApiController]
[Route("api/[controller]")]
public class MeController : ControllerBase
{
    [HttpGet]
    [Authorize]
    public IActionResult GetMe()
    {
        string userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        string username = User.FindFirstValue(ClaimTypes.Name)!;
        string email = User.FindFirstValue(ClaimTypes.Email)!;

        return Ok(new
        {
            userId,
            username,
            email
        });
    }
}