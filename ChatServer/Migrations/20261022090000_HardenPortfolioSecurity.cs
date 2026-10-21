using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ChatServer.Migrations;

public partial class HardenPortfolioSecurity : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AlterColumn<string>(
            name: "Status",
            table: "FriendRequests",
            type: "nvarchar(16)",
            maxLength: 16,
            nullable: false,
            oldClrType: typeof(string),
            oldType: "nvarchar(max)");

        migrationBuilder.AlterColumn<string>(
            name: "FileUrl",
            table: "Messages",
            type: "nvarchar(128)",
            maxLength: 128,
            nullable: true,
            oldClrType: typeof(string),
            oldType: "nvarchar(max)",
            oldNullable: true);

        migrationBuilder.AlterColumn<string>(
            name: "FileName",
            table: "Messages",
            type: "nvarchar(255)",
            maxLength: 255,
            nullable: true,
            oldClrType: typeof(string),
            oldType: "nvarchar(max)",
            oldNullable: true);

        migrationBuilder.AlterColumn<string>(
            name: "Content",
            table: "Messages",
            type: "nvarchar(4000)",
            maxLength: 4000,
            nullable: false,
            oldClrType: typeof(string),
            oldType: "nvarchar(max)");

        migrationBuilder.AlterColumn<string>(
            name: "Username",
            table: "Users",
            type: "nvarchar(20)",
            maxLength: 20,
            nullable: false,
            oldClrType: typeof(string),
            oldType: "nvarchar(max)");

        migrationBuilder.AlterColumn<string>(
            name: "PasswordHash",
            table: "Users",
            type: "nvarchar(255)",
            maxLength: 255,
            nullable: false,
            oldClrType: typeof(string),
            oldType: "nvarchar(max)");

        migrationBuilder.AlterColumn<string>(
            name: "Email",
            table: "Users",
            type: "nvarchar(254)",
            maxLength: 254,
            nullable: false,
            oldClrType: typeof(string),
            oldType: "nvarchar(max)");

        migrationBuilder.CreateTable(
            name: "UploadedFiles",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                OwnerId = table.Column<int>(type: "int", nullable: false),
                OriginalFileName = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                StoredFileName = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                ContentType = table.Column<string>(type: "nvarchar(127)", maxLength: 127, nullable: false),
                Size = table.Column<long>(type: "bigint", nullable: false),
                UploadedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_UploadedFiles", x => x.Id);
            });

        migrationBuilder.CreateIndex(
            name: "IX_FriendRequests_SenderId_ReceiverId_Status",
            table: "FriendRequests",
            columns: new[] { "SenderId", "ReceiverId", "Status" });

        migrationBuilder.CreateIndex(
            name: "IX_Messages_ReceiverId_IsRead",
            table: "Messages",
            columns: new[] { "ReceiverId", "IsRead" });

        migrationBuilder.CreateIndex(
            name: "IX_Messages_SenderId_ReceiverId_SentAt",
            table: "Messages",
            columns: new[] { "SenderId", "ReceiverId", "SentAt" });

        migrationBuilder.CreateIndex(
            name: "IX_UploadedFiles_OwnerId",
            table: "UploadedFiles",
            column: "OwnerId");

        migrationBuilder.CreateIndex(
            name: "IX_UploadedFiles_StoredFileName",
            table: "UploadedFiles",
            column: "StoredFileName",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_Users_Email",
            table: "Users",
            column: "Email",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_Users_Username",
            table: "Users",
            column: "Username",
            unique: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "UploadedFiles");
        migrationBuilder.DropIndex(name: "IX_FriendRequests_SenderId_ReceiverId_Status", table: "FriendRequests");
        migrationBuilder.DropIndex(name: "IX_Messages_ReceiverId_IsRead", table: "Messages");
        migrationBuilder.DropIndex(name: "IX_Messages_SenderId_ReceiverId_SentAt", table: "Messages");
        migrationBuilder.DropIndex(name: "IX_Users_Email", table: "Users");
        migrationBuilder.DropIndex(name: "IX_Users_Username", table: "Users");

        migrationBuilder.AlterColumn<string>(name: "Status", table: "FriendRequests", type: "nvarchar(max)", nullable: false, oldClrType: typeof(string), oldType: "nvarchar(16)", oldMaxLength: 16);
        migrationBuilder.AlterColumn<string>(name: "FileUrl", table: "Messages", type: "nvarchar(max)", nullable: true, oldClrType: typeof(string), oldType: "nvarchar(128)", oldMaxLength: 128, oldNullable: true);
        migrationBuilder.AlterColumn<string>(name: "FileName", table: "Messages", type: "nvarchar(max)", nullable: true, oldClrType: typeof(string), oldType: "nvarchar(255)", oldMaxLength: 255, oldNullable: true);
        migrationBuilder.AlterColumn<string>(name: "Content", table: "Messages", type: "nvarchar(max)", nullable: false, oldClrType: typeof(string), oldType: "nvarchar(4000)", oldMaxLength: 4000);
        migrationBuilder.AlterColumn<string>(name: "Username", table: "Users", type: "nvarchar(max)", nullable: false, oldClrType: typeof(string), oldType: "nvarchar(20)", oldMaxLength: 20);
        migrationBuilder.AlterColumn<string>(name: "PasswordHash", table: "Users", type: "nvarchar(max)", nullable: false, oldClrType: typeof(string), oldType: "nvarchar(255)", oldMaxLength: 255);
        migrationBuilder.AlterColumn<string>(name: "Email", table: "Users", type: "nvarchar(max)", nullable: false, oldClrType: typeof(string), oldType: "nvarchar(254)", oldMaxLength: 254);
    }
}
