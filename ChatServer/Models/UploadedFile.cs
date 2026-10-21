namespace ChatServer.Models;

/// <summary>
/// Metadata for a file accepted by the server but not necessarily sent in a chat yet.
/// Physical files stay outside wwwroot and are returned only to message participants.
/// </summary>
public class UploadedFile
{
    public Guid Id { get; set; }

    public int OwnerId { get; set; }

    [System.ComponentModel.DataAnnotations.MaxLength(255)]
    public string OriginalFileName { get; set; } = "";

    [System.ComponentModel.DataAnnotations.MaxLength(128)]
    public string StoredFileName { get; set; } = "";

    [System.ComponentModel.DataAnnotations.MaxLength(127)]
    public string ContentType { get; set; } = "application/octet-stream";

    public long Size { get; set; }

    public DateTime UploadedAt { get; set; } = DateTime.UtcNow;
}
