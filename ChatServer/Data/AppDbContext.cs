using ChatServer.Models;
using Microsoft.EntityFrameworkCore;

namespace ChatServer.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options)
        : base(options)
    {
    }
    public DbSet<FriendRequest> FriendRequests { get; set; }
    public DbSet<User> Users { get; set; }
    public DbSet<Message> Messages { get; set; }
    public DbSet<UploadedFile> UploadedFiles { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<User>(entity =>
        {
            entity.HasIndex(user => user.Username).IsUnique();
            entity.HasIndex(user => user.Email).IsUnique();
        });

        modelBuilder.Entity<FriendRequest>(entity =>
        {
            entity.HasIndex(request => new
            {
                request.SenderId,
                request.ReceiverId,
                request.Status
            });
        });

        modelBuilder.Entity<Message>(entity =>
        {
            entity.HasIndex(message => new
            {
                message.SenderId,
                message.ReceiverId,
                message.SentAt
            });
            entity.HasIndex(message => new
            {
                message.ReceiverId,
                message.IsRead
            });
        });

        modelBuilder.Entity<UploadedFile>(entity =>
        {
            entity.HasIndex(file => file.StoredFileName).IsUnique();
            entity.HasIndex(file => file.OwnerId);
        });
    }
}
