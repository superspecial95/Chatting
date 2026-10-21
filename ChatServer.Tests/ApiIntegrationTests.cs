using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using ChatServer.Data;
using ChatServer.Models;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace ChatServer.Tests;

public class ApiIntegrationTests
{
    private const string Password = "TestPassword123!";

    [Fact]
    public async Task Register_login_and_me_work_over_http()
    {
        using var factory = new MiniDiscordApiFactory();
        using var client = factory.CreateClient();

        (int userId, string token) = await RegisterAndLoginAsync(client, "alice");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        using var response = await client.GetAsync("/api/Me");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using JsonDocument body = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
        Assert.Equal(userId.ToString(), body.RootElement.GetProperty("userId").GetString());
        Assert.Equal("alice", body.RootElement.GetProperty("username").GetString());
        Assert.False(body.RootElement.TryGetProperty("passwordHash", out _));
    }

    [Fact]
    public async Task Registration_rejects_invalid_input_and_duplicates()
    {
        using var factory = new MiniDiscordApiFactory();
        using var client = factory.CreateClient();

        using var invalid = await client.PostAsJsonAsync("/api/Auth/register", new
        {
            username = "a!",
            email = "not-an-email",
            password = "short"
        });
        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);

        await RegisterAndLoginAsync(client, "alice");
        using var duplicate = await client.PostAsJsonAsync("/api/Auth/register", new
        {
            username = "alice",
            email = "different@example.test",
            password = Password
        });
        Assert.Equal(HttpStatusCode.Conflict, duplicate.StatusCode);

        using var duplicateEmail = await client.PostAsJsonAsync("/api/Auth/register", new
        {
            username = "charlie",
            email = "alice@example.test",
            password = Password
        });
        Assert.Equal(HttpStatusCode.Conflict, duplicateEmail.StatusCode);
    }

    [Fact]
    public async Task Login_rejects_wrong_password_and_private_api_requires_jwt()
    {
        using var factory = new MiniDiscordApiFactory();
        using var client = factory.CreateClient();
        await RegisterAndLoginAsync(client, "alice");

        using var wrongPassword = await client.PostAsJsonAsync("/api/Auth/login", new
        {
            username = "alice",
            password = "wrong-password"
        });
        Assert.Equal(HttpStatusCode.Unauthorized, wrongPassword.StatusCode);

        using var missingPassword = await client.PostAsJsonAsync("/api/Auth/login", new { username = "alice" });
        Assert.Equal(HttpStatusCode.BadRequest, missingPassword.StatusCode);

        using var noToken = await client.GetAsync("/api/Friends");
        Assert.Equal(HttpStatusCode.Unauthorized, noToken.StatusCode);
    }

    [Fact]
    public async Task Only_receiver_can_accept_friend_request()
    {
        using var factory = new MiniDiscordApiFactory();
        using var alice = factory.CreateClient();
        using var bob = factory.CreateClient();
        using var mallory = factory.CreateClient();
        (int aliceId, string aliceToken) = await RegisterAndLoginAsync(alice, "alice");
        (int bobId, string bobToken) = await RegisterAndLoginAsync(bob, "bob");
        (_, string malloryToken) = await RegisterAndLoginAsync(mallory, "mallory");
        SetToken(alice, aliceToken);
        SetToken(bob, bobToken);
        SetToken(mallory, malloryToken);

        using var sent = await alice.PostAsJsonAsync("/api/FriendRequests/send", new { username = "bob" });
        Assert.Equal(HttpStatusCode.OK, sent.StatusCode);
        using var received = await bob.GetAsync("/api/FriendRequests/received");
        Assert.Equal(HttpStatusCode.OK, received.StatusCode);
        using JsonDocument requests = await JsonDocument.ParseAsync(await received.Content.ReadAsStreamAsync());
        int requestId = requests.RootElement[0].GetProperty("requestId").GetInt32();

        using var wrongReceiver = await mallory.PostAsync($"/api/FriendRequests/accept/{requestId}", null);
        Assert.Equal(HttpStatusCode.NotFound, wrongReceiver.StatusCode);
        using var accepted = await bob.PostAsync($"/api/FriendRequests/accept/{requestId}", null);
        Assert.Equal(HttpStatusCode.OK, accepted.StatusCode);

        using var friends = await alice.GetAsync("/api/Friends");
        using JsonDocument friendList = await JsonDocument.ParseAsync(await friends.Content.ReadAsStreamAsync());
        Assert.Equal(bobId, friendList.RootElement[0].GetProperty("id").GetInt32());
        Assert.NotEqual(aliceId, bobId);
    }

    [Fact]
    public async Task Message_history_and_mark_read_require_accepted_friendship()
    {
        using var factory = new MiniDiscordApiFactory();
        using var alice = factory.CreateClient();
        using var bob = factory.CreateClient();
        (int aliceId, string aliceToken) = await RegisterAndLoginAsync(alice, "alice");
        (int bobId, string bobToken) = await RegisterAndLoginAsync(bob, "bob");
        SetToken(alice, aliceToken);
        SetToken(bob, bobToken);

        using var forbiddenHistory = await alice.GetAsync($"/api/Messages/{bobId}");
        Assert.Equal(HttpStatusCode.Forbidden, forbiddenHistory.StatusCode);
        using var forbiddenRead = await alice.PostAsync($"/api/Messages/mark-read/{bobId}", null);
        Assert.Equal(HttpStatusCode.Forbidden, forbiddenRead.StatusCode);

        await MakeFriendsAsync(alice, bob);
        using var allowedHistory = await alice.GetAsync($"/api/Messages/{bobId}");
        Assert.Equal(HttpStatusCode.OK, allowedHistory.StatusCode);
        using JsonDocument history = await JsonDocument.ParseAsync(await allowedHistory.Content.ReadAsStreamAsync());
        Assert.Equal(JsonValueKind.Array, history.RootElement.ValueKind);
        Assert.Equal(0, history.RootElement.GetArrayLength());
        Assert.NotEqual(aliceId, bobId);
    }

    [Fact]
    public async Task Upload_rejects_unsupported_executable()
    {
        using var factory = new MiniDiscordApiFactory();
        using var client = factory.CreateClient();
        (_, string token) = await RegisterAndLoginAsync(client, "alice");
        SetToken(client, token);

        using var multipart = new MultipartFormDataContent();
        multipart.Add(new ByteArrayContent([1, 2, 3]), "file", "unsafe.exe");
        using var response = await client.PostAsync("/api/Files/upload", multipart);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        using var emptyMultipart = new MultipartFormDataContent();
        emptyMultipart.Add(new ByteArrayContent([]), "file", "empty.txt");
        using var emptyResponse = await client.PostAsync("/api/Files/upload", emptyMultipart);
        Assert.Equal(HttpStatusCode.BadRequest, emptyResponse.StatusCode);
    }

    [Fact]
    public async Task Message_history_returns_latest_50_in_chronological_order()
    {
        using var factory = new MiniDiscordApiFactory();
        using var alice = factory.CreateClient();
        using var bob = factory.CreateClient();
        (int aliceId, string aliceToken) = await RegisterAndLoginAsync(alice, "alice");
        (int bobId, string bobToken) = await RegisterAndLoginAsync(bob, "bob");
        SetToken(alice, aliceToken);
        SetToken(bob, bobToken);
        await MakeFriendsAsync(alice, bob);

        using (var scope = factory.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            DateTime start = DateTime.UtcNow.AddHours(-1);
            for (int index = 0; index < 55; index++)
            {
                context.Messages.Add(new Message
                {
                    SenderId = aliceId,
                    ReceiverId = bobId,
                    Content = $"m{index:D2}",
                    SentAt = start.AddMinutes(index)
                });
            }

            await context.SaveChangesAsync();
        }

        using var response = await bob.GetAsync($"/api/Messages/{aliceId}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using JsonDocument history = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
        Assert.Equal(50, history.RootElement.GetArrayLength());
        Assert.Equal("m05", history.RootElement[0].GetProperty("message").GetString());
        Assert.Equal("m54", history.RootElement[49].GetProperty("message").GetString());
    }

    [Fact]
    public async Task SignalR_rejects_messages_to_non_friends()
    {
        using var factory = new MiniDiscordApiFactory();
        using var alice = factory.CreateClient();
        using var bob = factory.CreateClient();
        (_, string aliceToken) = await RegisterAndLoginAsync(alice, "alice");
        (int bobId, _) = await RegisterAndLoginAsync(bob, "bob");

        await using var aliceHub = CreateHubConnection(factory, aliceToken);
        await aliceHub.StartAsync();
        await Assert.ThrowsAsync<HubException>(() =>
            aliceHub.InvokeAsync("SendMessage", bobId, "not allowed"));
    }

    [Fact]
    public async Task SignalR_message_reaches_friend_and_is_saved_in_history()
    {
        using var factory = new MiniDiscordApiFactory();
        using var alice = factory.CreateClient();
        using var bob = factory.CreateClient();
        (int aliceId, string aliceToken) = await RegisterAndLoginAsync(alice, "alice");
        (int bobId, string bobToken) = await RegisterAndLoginAsync(bob, "bob");
        SetToken(alice, aliceToken);
        SetToken(bob, bobToken);
        await MakeFriendsAsync(alice, bob);

        await using var bobHub = CreateHubConnection(factory, bobToken);
        var received = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        bobHub.On<int, string, string>("ReceiveMessage", (senderId, _, message) =>
        {
            if (senderId == aliceId)
            {
                received.TrySetResult(message);
            }
        });
        await bobHub.StartAsync();

        await using var aliceHub = CreateHubConnection(factory, aliceToken);
        await aliceHub.StartAsync();
        await aliceHub.InvokeAsync("SendMessage", bobId, "hello from SignalR test");

        Assert.Equal("hello from SignalR test", await received.Task.WaitAsync(TimeSpan.FromSeconds(10)));
        using var historyResponse = await bob.GetAsync($"/api/Messages/{aliceId}");
        Assert.Equal(HttpStatusCode.OK, historyResponse.StatusCode);
        using JsonDocument history = await JsonDocument.ParseAsync(await historyResponse.Content.ReadAsStreamAsync());
        Assert.Contains(history.RootElement.EnumerateArray(), item =>
            item.GetProperty("message").GetString() == "hello from SignalR test");
    }

    [Fact]
    public async Task Shared_file_is_downloadable_only_by_message_participants()
    {
        using var factory = new MiniDiscordApiFactory();
        using var alice = factory.CreateClient();
        using var bob = factory.CreateClient();
        using var mallory = factory.CreateClient();
        using var anonymous = factory.CreateClient();
        (int aliceId, string aliceToken) = await RegisterAndLoginAsync(alice, "alice");
        (int bobId, string bobToken) = await RegisterAndLoginAsync(bob, "bob");
        (_, string malloryToken) = await RegisterAndLoginAsync(mallory, "mallory");
        SetToken(alice, aliceToken);
        SetToken(bob, bobToken);
        SetToken(mallory, malloryToken);

        byte[] contents = Encoding.UTF8.GetBytes("private test attachment");
        using var multipart = new MultipartFormDataContent();
        multipart.Add(new ByteArrayContent(contents), "file", "test.txt");
        using var uploaded = await alice.PostAsync("/api/Files/upload", multipart);
        Assert.Equal(HttpStatusCode.OK, uploaded.StatusCode);
        using JsonDocument uploadBody = await JsonDocument.ParseAsync(await uploaded.Content.ReadAsStreamAsync());
        Guid fileId = uploadBody.RootElement.GetProperty("fileId").GetGuid();

        string? createdFilePath = null;
        try
        {
            using (var scope = factory.Services.CreateScope())
            {
                var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                var environment = scope.ServiceProvider.GetRequiredService<IWebHostEnvironment>();
                var file = await context.UploadedFiles.FindAsync(fileId);
                Assert.NotNull(file);
                createdFilePath = Path.Combine(environment.ContentRootPath, "App_Data", "uploads", file.StoredFileName);
                context.Messages.Add(new ChatServer.Models.Message
                {
                    SenderId = aliceId,
                    ReceiverId = bobId,
                    FileUrl = $"/api/Files/{fileId}",
                    FileName = "test.txt"
                });
                await context.SaveChangesAsync();
            }

            using var denied = await mallory.GetAsync($"/api/Files/{fileId}");
            Assert.Equal(HttpStatusCode.NotFound, denied.StatusCode);
            using var noToken = await anonymous.GetAsync($"/api/Files/{fileId}");
            Assert.Equal(HttpStatusCode.Unauthorized, noToken.StatusCode);
            using var senderDownload = await alice.GetAsync($"/api/Files/{fileId}");
            Assert.Equal(HttpStatusCode.OK, senderDownload.StatusCode);
            Assert.Equal(contents, await senderDownload.Content.ReadAsByteArrayAsync());
            using var receiverDownload = await bob.GetAsync($"/api/Files/{fileId}");
            Assert.Equal(HttpStatusCode.OK, receiverDownload.StatusCode);
        }
        finally
        {
            if (createdFilePath != null && File.Exists(createdFilePath))
            {
                File.Delete(createdFilePath);
            }
        }
    }

    [Fact]
    public async Task Legacy_public_upload_path_is_blocked()
    {
        using var factory = new MiniDiscordApiFactory();
        using var client = factory.CreateClient();
        using var response = await client.GetAsync("/uploads/example.png");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    private static async Task<(int UserId, string Token)> RegisterAndLoginAsync(HttpClient client, string username)
    {
        using var registered = await client.PostAsJsonAsync("/api/Auth/register", new
        {
            username,
            email = $"{username}@example.test",
            password = Password
        });
        Assert.Equal(HttpStatusCode.OK, registered.StatusCode);
        using JsonDocument registerBody = await JsonDocument.ParseAsync(await registered.Content.ReadAsStreamAsync());
        Assert.False(registerBody.RootElement.TryGetProperty("passwordHash", out _));
        int userId = registerBody.RootElement.GetProperty("userId").GetInt32();

        using var loggedIn = await client.PostAsJsonAsync("/api/Auth/login", new { username, password = Password });
        Assert.Equal(HttpStatusCode.OK, loggedIn.StatusCode);
        using JsonDocument loginBody = await JsonDocument.ParseAsync(await loggedIn.Content.ReadAsStreamAsync());
        return (userId, loginBody.RootElement.GetProperty("token").GetString()!);
    }

    private static async Task MakeFriendsAsync(HttpClient alice, HttpClient bob)
    {
        using var sent = await alice.PostAsJsonAsync("/api/FriendRequests/send", new { username = "bob" });
        Assert.Equal(HttpStatusCode.OK, sent.StatusCode);
        using var received = await bob.GetAsync("/api/FriendRequests/received");
        using JsonDocument requests = await JsonDocument.ParseAsync(await received.Content.ReadAsStreamAsync());
        int requestId = requests.RootElement[0].GetProperty("requestId").GetInt32();
        using var accepted = await bob.PostAsync($"/api/FriendRequests/accept/{requestId}", null);
        Assert.Equal(HttpStatusCode.OK, accepted.StatusCode);
    }

    private static void SetToken(HttpClient client, string token) =>
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

    private static HubConnection CreateHubConnection(MiniDiscordApiFactory factory, string token) =>
        new HubConnectionBuilder()
            .WithUrl("http://localhost/chatHub", options =>
            {
                options.Transports = HttpTransportType.LongPolling;
                options.HttpMessageHandlerFactory = _ => factory.Server.CreateHandler();
                options.AccessTokenProvider = () => Task.FromResult<string?>(token);
            })
            .Build();
}
