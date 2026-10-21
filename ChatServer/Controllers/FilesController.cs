using System.Security.Claims;
using ChatServer.Data;
using ChatServer.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ChatServer.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class FilesController : ControllerBase
{
    private const long MaxFileSize = 25 * 1024 * 1024;

    private static readonly IReadOnlyDictionary<string, string> AllowedContentTypes =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        [".jpg"] = "image/jpeg",
        [".jpeg"] = "image/jpeg",
        [".png"] = "image/png",
        [".gif"] = "image/gif",
        [".webp"] = "image/webp",
        [".pdf"] = "application/pdf",
        [".txt"] = "text/plain",
        [".doc"] = "application/msword",
        [".docx"] = "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
        [".xls"] = "application/vnd.ms-excel",
        [".xlsx"] = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
        [".ppt"] = "application/vnd.ms-powerpoint",
        [".pptx"] = "application/vnd.openxmlformats-officedocument.presentationml.presentation",
        [".wav"] = "audio/wav",
        [".mp3"] = "audio/mpeg",
        [".m4a"] = "audio/mp4",
        [".aac"] = "audio/aac",
        [".ogg"] = "audio/ogg",
        [".zip"] = "application/zip"
    };

    private readonly IWebHostEnvironment _environment;
    private readonly AppDbContext _context;
    private readonly ILogger<FilesController> _logger;

    public FilesController(
        IWebHostEnvironment environment,
        AppDbContext context,
        ILogger<FilesController> logger)
    {
        _environment = environment;
        _context = context;
        _logger = logger;
    }

    [HttpPost("upload")]
    [RequestSizeLimit(MaxFileSize + 1_024_000)]
    public async Task<IActionResult> Upload(IFormFile? file)
    {
        int? userId = GetCurrentUserId();
        if (!userId.HasValue)
        {
            return Unauthorized();
        }

        if (file == null)
        {
            return BadRequest(new { message = "Server không nhận được tệp." });
        }

        if (file.Length <= 0)
        {
            return BadRequest(new { message = "Tệp có dung lượng 0 byte." });
        }

        if (file.Length > MaxFileSize)
        {
            return BadRequest(new { message = "Tệp không được vượt quá 25 MB." });
        }

        string originalFileName = Path.GetFileName(file.FileName);
        string extension = Path.GetExtension(originalFileName).ToLowerInvariant();

        if (string.IsNullOrWhiteSpace(originalFileName) ||
            !AllowedContentTypes.ContainsKey(extension))
        {
            return BadRequest(new
            {
                message = "Định dạng tệp không được hỗ trợ. Tệp thực thi và các định dạng không nằm trong danh sách cho phép bị từ chối."
            });
        }

        string contentType = AllowedContentTypes[extension];

        string storageFolder = Path.Combine(_environment.ContentRootPath, "App_Data", "uploads");
        Directory.CreateDirectory(storageFolder);

        var uploadedFile = new UploadedFile
        {
            Id = Guid.NewGuid(),
            OwnerId = userId.Value,
            OriginalFileName = originalFileName,
            StoredFileName = $"{Guid.NewGuid():N}{extension}",
            ContentType = contentType,
            Size = file.Length,
            UploadedAt = DateTime.UtcNow
        };

        string filePath = Path.Combine(storageFolder, uploadedFile.StoredFileName);

        try
        {
            await using var stream = new FileStream(
                filePath,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None);

            await file.CopyToAsync(stream);

            _context.UploadedFiles.Add(uploadedFile);
            await _context.SaveChangesAsync();
        }
        catch (Exception exception)
        {
            try
            {
                if (System.IO.File.Exists(filePath))
                {
                    System.IO.File.Delete(filePath);
                }
            }
            catch (IOException cleanupException)
            {
                _logger.LogWarning(cleanupException, "Could not clean up failed upload {FilePath}", filePath);
            }

            _logger.LogError(exception, "Could not store upload for user {UserId}", userId.Value);
            return StatusCode(StatusCodes.Status500InternalServerError, new
            {
                message = "Không thể lưu tệp. Vui lòng thử lại."
            });
        }

        return Ok(new
        {
            fileId = uploadedFile.Id,
            fileName = uploadedFile.OriginalFileName
        });
    }

    [HttpGet("{fileId:guid}")]
    public async Task<IActionResult> Download(Guid fileId)
    {
        int? userId = GetCurrentUserId();
        if (!userId.HasValue)
        {
            return Unauthorized();
        }

        string fileUrl = $"/api/Files/{fileId}";
        bool mayReadFile = await _context.Messages.AnyAsync(message =>
            message.FileUrl == fileUrl &&
            (message.SenderId == userId.Value || message.ReceiverId == userId.Value));

        if (!mayReadFile)
        {
            return NotFound();
        }

        var uploadedFile = await _context.UploadedFiles.FindAsync(fileId);
        if (uploadedFile == null)
        {
            return NotFound();
        }

        string filePath = Path.Combine(
            _environment.ContentRootPath,
            "App_Data",
            "uploads",
            uploadedFile.StoredFileName);

        if (!System.IO.File.Exists(filePath))
        {
            _logger.LogWarning("Upload metadata exists but file is missing: {FileId}", fileId);
            return NotFound();
        }

        return PhysicalFile(
            filePath,
            uploadedFile.ContentType,
            uploadedFile.OriginalFileName,
            enableRangeProcessing: true);
    }

    // Files uploaded before private storage was introduced still live in wwwroot/uploads.
    // Keep their original message URLs in the database, but serve them only to participants.
    [HttpGet("legacy/{storedFileName}")]
    public async Task<IActionResult> DownloadLegacy(string storedFileName)
    {
        int? userId = GetCurrentUserId();
        if (!userId.HasValue)
        {
            return Unauthorized();
        }

        string extension = Path.GetExtension(storedFileName);
        string stem = Path.GetFileNameWithoutExtension(storedFileName);
        if (storedFileName != Path.GetFileName(storedFileName) ||
            !Guid.TryParseExact(stem, "D", out _) ||
            extension.Length is < 2 or > 11 ||
            !extension.AsSpan(1).ToString().All(char.IsAsciiLetterOrDigit))
        {
            return NotFound();
        }

        string legacyUrl = $"/uploads/{storedFileName}";
        var message = await _context.Messages
            .AsNoTracking()
            .Where(item => item.FileUrl == legacyUrl &&
                (item.SenderId == userId.Value || item.ReceiverId == userId.Value))
            .Select(item => new { item.FileName })
            .FirstOrDefaultAsync();

        if (message == null)
        {
            return NotFound();
        }

        string filePath = Path.Combine(
            _environment.ContentRootPath,
            "wwwroot",
            "uploads",
            storedFileName);

        if (!System.IO.File.Exists(filePath))
        {
            _logger.LogWarning("Legacy attachment is missing: {StoredFileName}", storedFileName);
            return NotFound();
        }

        string contentType = AllowedContentTypes.TryGetValue(extension, out string? knownType)
            ? knownType
            : "application/octet-stream";
        string downloadName = Path.GetFileName(message.FileName ?? storedFileName);

        return PhysicalFile(filePath, contentType, downloadName, enableRangeProcessing: true);
    }

    private int? GetCurrentUserId()
    {
        string? value = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return int.TryParse(value, out int userId) ? userId : null;
    }
}
