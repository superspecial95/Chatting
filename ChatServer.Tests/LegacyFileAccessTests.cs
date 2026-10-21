using System.Security.Claims;
using ChatServer.Controllers;
using ChatServer.Data;
using ChatServer.Models;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace ChatServer.Tests;

public class LegacyFileAccessTests
{
    [Fact]
    public async Task Legacy_file_is_available_to_message_participants_only()
    {
        string root = Path.Combine(Path.GetTempPath(), "MiniDiscordLegacyFileTests", Guid.NewGuid().ToString("N"));
        string folder = Path.Combine(root, "wwwroot", "uploads");
        Directory.CreateDirectory(folder);

        try
        {
            string storedFileName = $"{Guid.NewGuid():D}.png";
            string filePath = Path.Combine(folder, storedFileName);
            await File.WriteAllBytesAsync(filePath, [1, 2, 3]);

            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options;
            await using var context = new AppDbContext(options);
            context.Messages.Add(new Message
            {
                SenderId = 1,
                ReceiverId = 2,
                FileUrl = $"/uploads/{storedFileName}",
                FileName = "photo.png"
            });
            await context.SaveChangesAsync();

            var controller = new FilesController(
                new TestWebHostEnvironment(root),
                context,
                NullLogger<FilesController>.Instance);

            SetUser(controller, 3);
            Assert.IsType<NotFoundResult>(await controller.DownloadLegacy(storedFileName));

            SetUser(controller, 1);
            var result = Assert.IsType<PhysicalFileResult>(await controller.DownloadLegacy(storedFileName));
            Assert.Equal(filePath, result.FileName);
            Assert.Equal("photo.png", result.FileDownloadName);

            SetUser(controller, 2);
            Assert.IsType<PhysicalFileResult>(await controller.DownloadLegacy(storedFileName));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    private static void SetUser(ControllerBase controller, int userId)
    {
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(
                    [new Claim(ClaimTypes.NameIdentifier, userId.ToString())],
                    "Test"))
            }
        };
    }

    private sealed class TestWebHostEnvironment(string contentRootPath) : IWebHostEnvironment
    {
        public string ApplicationName { get; set; } = "ChatServer.Tests";
        public string EnvironmentName { get; set; } = "Development";
        public string ContentRootPath { get; set; } = contentRootPath;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
        public string WebRootPath { get; set; } = Path.Combine(contentRootPath, "wwwroot");
        public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();
    }
}
