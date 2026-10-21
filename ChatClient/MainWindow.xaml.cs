using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Win32;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using NAudio.Wave;

namespace ChatClient;

public partial class MainWindow : Window
{
    private readonly HttpClient _httpClient;

    private HubConnection? _connection;

    private int? _selectedUserId;

    private WaveIn? _voiceRecorder;

    private WaveFileWriter? _voiceWriter;

    private string? _voiceRecordingPath;

    private int? _voiceRecordingReceiverId;

    private string? _voicePlaybackPath;

    private readonly MediaPlayer _voicePlayer = new();

    public MainWindow()
    {
        InitializeComponent();

        _httpClient = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(30)
        };

        UserText.Text =
            $"Xin chào, {Session.Username}!";

        UsersListBox.SelectionChanged +=
            UsersListBox_SelectionChanged;

        Loaded += MainWindow_Loaded;
    }

    private async void UsersListBox_SelectionChanged(
        object sender,
        SelectionChangedEventArgs e)
    {
        if (UsersListBox.SelectedItem
            is UserStatusResponse user)
        {
            _selectedUserId = user.Id;

            user.UnreadCount = 0;

            UsersListBox.Items.Refresh();

            ChatPanel.Children.Clear();

            AddChatHeader(
                $"Đang chat với {user.Username}");

            await MarkMessagesAsReadAsync(
                user.Id);

            await LoadMessageHistoryAsync(
                user.Id);
        }
    }

    private void AddChatHeader(
        string text)
    {
        TextBlock header =
            new TextBlock
            {
                Text = text,
                FontSize = 16,
                FontWeight = FontWeights.Bold,
                Foreground = Brushes.Gray,
                HorizontalAlignment =
                    HorizontalAlignment.Center,
                Margin =
                    new Thickness(0, 8, 0, 8)
            };

        ChatPanel.Children.Add(header);
    }

    private async void MainWindow_Loaded(
        object sender,
        RoutedEventArgs e)
    {
        try
        {
            _connection =
                new HubConnectionBuilder()
                    .WithUrl(
                        $"{ServerConfiguration.ServerUrl}/chatHub",
                        options =>
                        {
                            options.AccessTokenProvider =
                                () =>
                                    Task.FromResult<string?>(
                                        Session.Token);
                        })
                    .WithAutomaticReconnect()
                    .Build();

            _connection.On<int, string, string>(
                "ReceiveMessage",
                (
                    senderId,
                    username,
                    message) =>
                {
                    Dispatcher.Invoke(
                        () =>
                        {
                            if (senderId ==
                                Session.UserId)
                            {
                                if (_selectedUserId ==
                                    null)
                                    return;

                                AddTextMessage(
                                    username,
                                    message,
                                    true);

                                ScrollChatToBottom();

                                return;
                            }

                            if (_selectedUserId !=
                                senderId)
                            {
                                var user =
                                    UsersListBox.Items
                                        .OfType<UserStatusResponse>()
                                        .FirstOrDefault(
                                            u =>
                                                u.Id ==
                                                senderId);

                                if (user != null)
                                {
                                    user.UnreadCount++;

                                    UsersListBox
                                        .Items
                                        .Refresh();
                                }

                                return;
                            }

                            AddTextMessage(
                                username,
                                message,
                                false);

                            ScrollChatToBottom();
                        });
                });

            _connection.On<
                int,
                string,
                string,
                string>(
                "ReceiveFile",
                (
                    senderId,
                    username,
                    fileName,
                    fileUrl) =>
                {
                    Dispatcher.Invoke(
                        () =>
                        {
                            if (senderId ==
                                Session.UserId)
                            {
                                if (_selectedUserId ==
                                    null)
                                    return;

                                AddFileMessage(
                                    username,
                                    fileName,
                                    fileUrl,
                                    true);

                                ScrollChatToBottom();

                                return;
                            }

                            if (_selectedUserId !=
                                senderId)
                            {
                                var user =
                                    UsersListBox.Items
                                        .OfType<UserStatusResponse>()
                                        .FirstOrDefault(
                                            u =>
                                                u.Id ==
                                                senderId);

                                if (user != null)
                                {
                                    user.UnreadCount++;

                                    UsersListBox
                                        .Items
                                        .Refresh();
                                }

                                return;
                            }

                            AddFileMessage(
                                username,
                                fileName,
                                fileUrl,
                                false);

                            ScrollChatToBottom();
                        });
                });

            _connection.On<int, bool>(
                "UserStatusChanged",
                (
                    userId,
                    isOnline) =>
                {
                    Dispatcher.Invoke(
                        () =>
                        {
                            var user =
                                UsersListBox.Items
                                    .OfType<UserStatusResponse>()
                                    .FirstOrDefault(
                                        u =>
                                            u.Id ==
                                            userId);

                            if (user == null)
                                return;

                            user.IsOnline =
                                isOnline;

                            UsersListBox
                                .Items
                                .Refresh();
                        });
                });

            _connection.On<int, string>(
                "FriendRequestReceived",
                (
                    senderId,
                    senderUsername) =>
                {
                    Dispatcher.Invoke(
                        async () =>
                        {
                            StatusText.Text =
                                $"{senderUsername} đã gửi lời mời kết bạn!";

                            MessageBox.Show(
                                $"{senderUsername} đã gửi lời mời kết bạn!",
                                "Lời mời kết bạn",
                                MessageBoxButton.OK,
                                MessageBoxImage.Information);

                            await LoadFriendRequestsAsync();
                        });
                });

            _connection.On<int, string>(
                "FriendRequestAccepted",
                (
                    friendId,
                    friendUsername) =>
                {
                    Dispatcher.Invoke(
                        async () =>
                        {
                            StatusText.Text =
                                $"{friendUsername} đã chấp nhận lời mời kết bạn!";

                            MessageBox.Show(
                                $"{friendUsername} đã chấp nhận lời mời kết bạn!",
                                "Kết bạn thành công",
                                MessageBoxButton.OK,
                                MessageBoxImage.Information);

                            await LoadUsersAsync();
                        });
                });

            await _connection.StartAsync();

            StatusText.Text =
                "Đã kết nối Chat Server!";

            await LoadUsersAsync();

            await LoadFriendRequestsAsync();
        }
        catch (Exception ex)
        {
            StatusText.Text =
                "Không thể kết nối Chat Server:\n" +
                ex.Message;
        }
    }

    private async void AddFriendButton_Click(
        object sender,
        RoutedEventArgs e)
    {
        string username =
            FriendUsernameTextBox.Text.Trim();

        if (string.IsNullOrWhiteSpace(username))
        {
            StatusText.Text =
                "Hãy nhập username.";

            return;
        }

        try
        {
            SetAuthorizationHeader();

            var requestData =
                new SendFriendRequestRequest
                {
                    Username = username
                };

            string json =
                JsonSerializer.Serialize(
                    requestData);

            using var content =
                new StringContent(
                    json,
                    Encoding.UTF8,
                    "application/json");

            HttpResponseMessage response =
                await _httpClient.PostAsync(
                    $"{ServerConfiguration.ServerUrl}/api/FriendRequests/send",
                    content);

            string result =
                await response.Content
                    .ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                StatusText.Text =
                    result;

                return;
            }

            FriendUsernameTextBox.Clear();

            StatusText.Text =
                "Đã gửi lời mời kết bạn.";
        }
        catch (Exception ex)
        {
            StatusText.Text =
                "Lỗi gửi lời mời:\n" +
                ex.Message;
        }
    }

    private async void SendMessageButton_Click(
        object sender,
        RoutedEventArgs e)
    {
        if (_connection == null)
        {
            StatusText.Text =
                "Chưa kết nối Server.";

            return;
        }

        if (_selectedUserId == null)
        {
            StatusText.Text =
                "Hãy chọn một người để chat.";

            return;
        }

        string message =
            MessageTextBox.Text.Trim();

        if (string.IsNullOrWhiteSpace(message))
            return;

        try
        {
            await _connection.InvokeAsync(
                "SendMessage",
                _selectedUserId.Value,
                message);

            MessageTextBox.Clear();
        }
        catch (Exception ex)
        {
            StatusText.Text =
                "Gửi tin nhắn thất bại:\n" +
                ex.Message;
        }
    }

    private async void SendFileButton_Click(
        object sender,
        RoutedEventArgs e)
    {
        if (_connection == null)
        {
            StatusText.Text =
                "Chưa kết nối Server.";

            return;
        }

        if (_selectedUserId == null)
        {
            StatusText.Text =
                "Hãy chọn một người để chat.";

            return;
        }

        OpenFileDialog dialog =
            new OpenFileDialog();

        if (dialog.ShowDialog() != true)
            return;

        try
        {
            StatusText.Text =
                "Đang upload file...";

            SetAuthorizationHeader();

            using var form =
                new MultipartFormDataContent();

            await using var fileStream =
                new FileStream(
                    dialog.FileName,
                    FileMode.Open,
                    FileAccess.Read);

            using var fileContent =
                new StreamContent(fileStream);

            fileContent.Headers.ContentType =
                new MediaTypeHeaderValue(
                    "application/octet-stream");

            form.Add(
                fileContent,
                "file",
                Path.GetFileName(dialog.FileName));

            HttpResponseMessage response =
                await _httpClient.PostAsync(
                    $"{ServerConfiguration.ServerUrl}/api/Files/upload",
                    form);

            string resultText =
                await response.Content
                    .ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                StatusText.Text =
                    $"Upload thất bại: {resultText}";

                return;
            }

            var uploadResult =
                JsonSerializer.Deserialize<FileUploadResponse>(
                    resultText,
                    new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    });

            if (uploadResult == null)
            {
                StatusText.Text =
                    "Server trả dữ liệu upload không hợp lệ.";

                return;
            }

            StatusText.Text =
                "Đang gửi file vào chat...";

            await _connection.InvokeAsync(
                "SendFile",
                _selectedUserId.Value,
                uploadResult.FileId);

            StatusText.Text =
                "Đã gửi file.";
        }
        catch (Exception ex)
        {
            StatusText.Text =
                "Gửi file thất bại:\n" +
                ex.Message;
        }
    }

    private void RecordVoiceButton_Click(
        object sender,
        RoutedEventArgs e)
    {
        if (_voiceRecorder != null)
        {
            RecordVoiceButton.IsEnabled = false;
            StatusText.Text = "Đang hoàn tất ghi âm...";
            _voiceRecorder.StopRecording();
            return;
        }

        if (_selectedUserId == null)
        {
            StatusText.Text = "Hãy chọn một người để gửi tin nhắn thoại.";
            return;
        }

        if (_connection == null ||
            _connection.State != HubConnectionState.Connected)
        {
            StatusText.Text = "Chưa kết nối Chat Server.";
            return;
        }

        try
        {
            _voiceRecordingReceiverId = _selectedUserId;
            _voiceRecordingPath = Path.Combine(
                Path.GetTempPath(),
                $"mini-discord-voice-{Guid.NewGuid():N}.wav");

            _voiceRecorder = new WaveIn
            {
                WaveFormat = new WaveFormat(16_000, 16, 1),
                BufferMilliseconds = 100
            };

            _voiceWriter = new WaveFileWriter(
                _voiceRecordingPath,
                _voiceRecorder.WaveFormat);

            _voiceRecorder.DataAvailable +=
                VoiceRecorder_DataAvailable;

            _voiceRecorder.RecordingStopped +=
                VoiceRecorder_RecordingStopped;

            _voiceRecorder.StartRecording();

            RecordVoiceButton.Content = "■ Dừng ghi âm";
            StatusText.Text = "Đang ghi âm... Bấm ‘Dừng ghi âm’ để gửi.";
        }
        catch (Exception ex)
        {
            DisposeVoiceRecording();
            StatusText.Text = "Không thể dùng microphone:\n" + ex.Message;
        }
    }

    private void VoiceRecorder_DataAvailable(
        object? sender,
        WaveInEventArgs e)
    {
        _voiceWriter?.Write(
            e.Buffer,
            0,
            e.BytesRecorded);
    }

    private async void VoiceRecorder_RecordingStopped(
        object? sender,
        StoppedEventArgs e)
    {
        await FinishVoiceRecordingAsync(e.Exception);
    }

    private async Task FinishVoiceRecordingAsync(
        Exception? recordingError)
    {
        string? recordingPath = _voiceRecordingPath;
        int? receiverId = _voiceRecordingReceiverId;

        DisposeVoiceRecording();

        await Dispatcher.InvokeAsync(() =>
        {
            RecordVoiceButton.IsEnabled = true;
            RecordVoiceButton.Content = "🎙 Ghi âm";
        });

        if (recordingError != null)
        {
            await Dispatcher.InvokeAsync(() =>
            {
                StatusText.Text = "Ghi âm thất bại:\n" + recordingError.Message;
            });
            DeleteTemporaryVoiceFile(recordingPath);
            return;
        }

        if (recordingPath == null || !receiverId.HasValue ||
            !File.Exists(recordingPath) ||
            new FileInfo(recordingPath).Length <= 44)
        {
            await Dispatcher.InvokeAsync(() =>
            {
                StatusText.Text = "Chưa ghi được nội dung thoại. Hãy thử lại.";
            });
            DeleteTemporaryVoiceFile(recordingPath);
            return;
        }

        try
        {
            await SendVoiceMessageAsync(
                recordingPath,
                receiverId.Value);
        }
        finally
        {
            DeleteTemporaryVoiceFile(recordingPath);
        }
    }

    private async Task SendVoiceMessageAsync(
        string recordingPath,
        int receiverId)
    {
        HubConnection? connection = _connection;

        if (connection == null ||
            connection.State != HubConnectionState.Connected)
        {
            StatusText.Text = "Mất kết nối trước khi gửi tin nhắn thoại.";
            return;
        }

        try
        {
            StatusText.Text = "Đang tải tin nhắn thoại lên...";
            SetAuthorizationHeader();

            using var form = new MultipartFormDataContent();
            await using var fileStream = new FileStream(
                recordingPath,
                FileMode.Open,
                FileAccess.Read);
            using var fileContent = new StreamContent(fileStream);

            fileContent.Headers.ContentType =
                new MediaTypeHeaderValue("audio/wav");

            string fileName =
                $"voice-{DateTime.Now:yyyyMMdd-HHmmss}.wav";

            form.Add(fileContent, "file", fileName);

            HttpResponseMessage response =
                await _httpClient.PostAsync(
                    $"{ServerConfiguration.ServerUrl}/api/Files/upload",
                    form);

            string resultText =
                await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                StatusText.Text =
                    $"Tải tin nhắn thoại thất bại: {resultText}";
                return;
            }

            var uploadResult =
                JsonSerializer.Deserialize<FileUploadResponse>(
                    resultText,
                    new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    });

            if (uploadResult == null)
            {
                StatusText.Text =
                    "Server trả dữ liệu tin nhắn thoại không hợp lệ.";
                return;
            }

            await connection.InvokeAsync(
                "SendFile",
                receiverId,
                uploadResult.FileId);

            StatusText.Text = "Đã gửi tin nhắn thoại.";
        }
        catch (Exception ex)
        {
            StatusText.Text = "Gửi tin nhắn thoại thất bại:\n" + ex.Message;
        }
    }

    private void DisposeVoiceRecording()
    {
        _voiceWriter?.Dispose();
        _voiceWriter = null;

        _voiceRecorder?.Dispose();
        _voiceRecorder = null;

        _voiceRecordingPath = null;
        _voiceRecordingReceiverId = null;
    }

    private void DeleteTemporaryVoiceFile(
        string? recordingPath)
    {
        if (string.IsNullOrWhiteSpace(recordingPath))
            return;

        try
        {
            if (File.Exists(recordingPath))
                File.Delete(recordingPath);
        }
        catch
        {
            // Temporary audio is removed on a later system cleanup if it is locked.
        }
    }

    private async Task LoadMessageHistoryAsync(
        int otherUserId)
    {
        try
        {
            SetAuthorizationHeader();

            HttpResponseMessage response =
                await _httpClient.GetAsync(
                    $"{ServerConfiguration.ServerUrl}/api/Messages/{otherUserId}");

            if (!response.IsSuccessStatusCode)
            {
                StatusText.Text =
                    "Không thể tải lịch sử tin nhắn.";

                return;
            }

            string result =
                await response.Content
                    .ReadAsStringAsync();

            var messages =
                JsonSerializer.Deserialize<
                    List<MessageHistoryResponse>>(
                    result,
                    new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    });

            if (messages == null)
                return;

            foreach (var item in messages)
            {
                bool isMine =
                    item.SenderId ==
                    Session.UserId;

                if (!string.IsNullOrWhiteSpace(
                        item.FileUrl))
                {
                    AddFileMessage(
                        item.Username,
                        item.FileName ?? "File",
                        item.FileUrl,
                        isMine);

                    continue;
                }

                if (!string.IsNullOrWhiteSpace(
                        item.Message))
                {
                    AddTextMessage(
                        item.Username,
                        item.Message,
                        isMine);
                }
            }

            ScrollChatToBottom();
        }
        catch (Exception ex)
        {
            StatusText.Text =
                "Lỗi tải lịch sử chat:\n" +
                ex.Message;
        }
    }

    private async Task LoadUnreadCountsAsync()
    {
        try
        {
            SetAuthorizationHeader();

            HttpResponseMessage response =
                await _httpClient.GetAsync(
                    $"{ServerConfiguration.ServerUrl}/api/Messages/unread-counts");

            if (!response.IsSuccessStatusCode)
                return;

            string result =
                await response.Content
                    .ReadAsStringAsync();

            var unreadCounts =
                JsonSerializer.Deserialize<
                    List<UnreadCountResponse>>(
                    result,
                    new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    });

            if (unreadCounts == null)
                return;

            foreach (var user in
                UsersListBox.Items
                    .OfType<UserStatusResponse>())
            {
                var unread =
                    unreadCounts.FirstOrDefault(
                        x =>
                            x.UserId ==
                            user.Id);

                user.UnreadCount =
                    unread?.Count ?? 0;
            }

            UsersListBox.Items.Refresh();
        }
        catch
        {
        }
    }

    private async Task MarkMessagesAsReadAsync(
        int otherUserId)
    {
        try
        {
            SetAuthorizationHeader();

            await _httpClient.PostAsync(
                $"{ServerConfiguration.ServerUrl}/api/Messages/mark-read/{otherUserId}",
                null);
        }
        catch
        {
        }
    }

    private void AddTextMessage(
        string username,
        string message,
        bool isMine)
    {
        Border bubble =
            new Border
            {
                Background =
                    isMine
                        ? Brushes.LightBlue
                        : Brushes.LightGray,

                CornerRadius =
                    new CornerRadius(12),

                Padding =
                    new Thickness(12, 8, 12, 8),

                Margin =
                    new Thickness(5, 3, 5, 3),

                MaxWidth = 450,

                HorizontalAlignment =
                    isMine
                        ? HorizontalAlignment.Right
                        : HorizontalAlignment.Left
            };

        StackPanel messagePanel =
            new StackPanel();

        TextBlock senderText =
            new TextBlock
            {
                Text =
                    isMine
                        ? "Bạn"
                        : username,

                FontSize = 12,

                FontWeight =
                    FontWeights.Bold,

                Foreground =
                    Brushes.DarkGray,

                Margin =
                    new Thickness(0, 0, 0, 3)
            };

        TextBlock messageText =
            new TextBlock
            {
                Text = message,

                FontSize = 15,

                TextWrapping =
                    TextWrapping.Wrap
            };

        messagePanel.Children.Add(senderText);
        messagePanel.Children.Add(messageText);

        bubble.Child =
            messagePanel;

        ChatPanel.Children.Add(
            bubble);
    }

    private void AddFileMessage(
        string username,
        string fileName,
        string fileUrl,
        bool isMine)
    {
        if (string.IsNullOrWhiteSpace(fileUrl))
            return;

        // File URLs are issued by FilesController. Never fetch arbitrary URLs from chat data:
        // doing so could leak the bearer token added by DownloadFileAsync.
        if (fileUrl.StartsWith("/uploads/", StringComparison.OrdinalIgnoreCase))
        {
            string storedFileName = fileUrl["/uploads/".Length..];
            string legacyExtension = Path.GetExtension(storedFileName);
            if (storedFileName != Path.GetFileName(storedFileName) ||
                !Guid.TryParseExact(Path.GetFileNameWithoutExtension(storedFileName), "D", out _) ||
                legacyExtension.Length is < 2 or > 11 ||
                !legacyExtension[1..].All(char.IsAsciiLetterOrDigit))
            {
                StatusText.Text = "Đường dẫn tệp cũ không hợp lệ.";
                return;
            }

            fileUrl = $"/api/Files/legacy/{storedFileName}";
        }

        if (!fileUrl.StartsWith("/api/Files/", StringComparison.OrdinalIgnoreCase))
        {
            StatusText.Text = "Tệp này dùng định dạng cũ hoặc không hợp lệ nên không thể mở an toàn.";
            return;
        }

        string fullUrl = $"{ServerConfiguration.ServerUrl}{fileUrl}";

        string extension =
            Path.GetExtension(fileName)
                .ToLowerInvariant();

        if (IsImageFile(extension))
        {
            AddImageMessage(
                username,
                fileName,
                fullUrl,
                isMine);

            return;
        }

        if (IsAudioFile(extension))
        {
            AddVoiceMessage(
                username,
                fileName,
                fullUrl,
                isMine);

            return;
        }

        AddOtherFileMessage(
            username,
            fileName,
            fullUrl,
            extension,
            isMine);
    }

    private void AddVoiceMessage(
        string username,
        string fileName,
        string fullUrl,
        bool isMine)
    {
        Border bubble = new Border
        {
            Background =
                isMine
                    ? Brushes.LightBlue
                    : Brushes.LightGray,

            CornerRadius = new CornerRadius(12),
            Padding = new Thickness(10),
            Margin = new Thickness(5, 3, 5, 3),
            MaxWidth = 420,
            HorizontalAlignment =
                isMine
                    ? HorizontalAlignment.Right
                    : HorizontalAlignment.Left
        };

        StackPanel content = new StackPanel();

        TextBlock title = new TextBlock
        {
            Text = isMine
                ? "Bạn: 🎙 Tin nhắn thoại"
                : $"{username}: 🎙 Tin nhắn thoại",
            FontSize = 13,
            FontWeight = FontWeights.SemiBold
        };

        TextBlock name = new TextBlock
        {
            Text = fileName,
            FontSize = 11,
            Foreground = Brushes.DimGray,
            Margin = new Thickness(0, 3, 0, 7),
            TextWrapping = TextWrapping.Wrap
        };

        StackPanel actions = new StackPanel
        {
            Orientation = Orientation.Horizontal
        };

        Button playButton = new Button
        {
            Content = "▶ Phát",
            MinWidth = 72,
            Height = 30,
            Margin = new Thickness(0, 0, 6, 0)
        };

        playButton.Click += async (sender, eventArgs) =>
        {
            await PlayVoiceMessageAsync(fullUrl, Path.GetExtension(fileName));
        };

        Button stopButton = new Button
        {
            Content = "■ Dừng",
            MinWidth = 72,
            Height = 30,
            Margin = new Thickness(0, 0, 6, 0)
        };

        stopButton.Click += (sender, eventArgs) =>
        {
            _voicePlayer.Stop();
        };

        Button downloadButton = new Button
        {
            Content = "⬇ Tải xuống",
            MinWidth = 98,
            Height = 30
        };

        downloadButton.Click += async (sender, eventArgs) =>
        {
            await DownloadFileAsync(
                fullUrl,
                fileName,
                "Lưu tin nhắn thoại");
        };

        actions.Children.Add(playButton);
        actions.Children.Add(stopButton);
        actions.Children.Add(downloadButton);
        content.Children.Add(title);
        content.Children.Add(name);
        content.Children.Add(actions);
        bubble.Child = content;
        ChatPanel.Children.Add(bubble);
    }

    private void AddImageMessage(
        string username,
        string fileName,
        string fullUrl,
        bool isMine)
    {
        Border outerBubble =
            new Border
            {
                Background =
                    isMine
                        ? Brushes.LightBlue
                        : Brushes.LightGray,

                CornerRadius =
                    new CornerRadius(12),

                Padding =
                    new Thickness(10),

                Margin =
                    new Thickness(5, 3, 5, 3),

                MaxWidth = 380,

                HorizontalAlignment =
                    isMine
                        ? HorizontalAlignment.Right
                        : HorizontalAlignment.Left
            };

        StackPanel container =
            new StackPanel();

        TextBlock nameText =
            new TextBlock
            {
                Text =
                    isMine
                        ? $"Bạn: 🖼️ {fileName}"
                        : $"{username}: 🖼️ {fileName}",

                FontSize = 14,

                FontWeight =
                    FontWeights.SemiBold,

                TextWrapping =
                    TextWrapping.Wrap,

                Margin =
                    new Thickness(0, 0, 0, 5)
            };

        Border imageBorder =
            new Border
            {
                BorderBrush =
                    Brushes.LightGray,

                BorderThickness =
                    new Thickness(1),

                CornerRadius =
                    new CornerRadius(5),

                Padding =
                    new Thickness(3),

                HorizontalAlignment =
                    HorizontalAlignment.Left
            };

        Image image =
            new Image
            {
                MaxWidth = 350,

                MaxHeight = 250,

                Stretch =
                    Stretch.Uniform,

                Cursor =
                    System.Windows.Input.Cursors.Hand
            };

        image.MouseLeftButtonUp +=
            async (s, e) =>
            {
                await DownloadFileAsync(
                    fullUrl,
                    fileName,
                    "Lưu hình ảnh");
            };

        imageBorder.Child =
            image;

        Button downloadButton =
            new Button
            {
                Content = "⬇ Tải xuống",

                Width = 110,

                Height = 32,

                HorizontalAlignment =
                    HorizontalAlignment.Left,

                Margin =
                    new Thickness(0, 5, 0, 0),

                Cursor =
                    System.Windows.Input.Cursors.Hand
            };

        downloadButton.Click +=
            async (s, e) =>
            {
                await DownloadFileAsync(
                    fullUrl,
                    fileName,
                    "Lưu hình ảnh");
            };

        container.Children.Add(nameText);
        container.Children.Add(imageBorder);
        container.Children.Add(downloadButton);

        outerBubble.Child =
            container;

        ChatPanel.Children.Add(
            outerBubble);

        _ = LoadProtectedImageAsync(image, fullUrl);
    }

    private void AddOtherFileMessage(
        string username,
        string fileName,
        string fullUrl,
        string extension,
        bool isMine)
    {
        Border outerBubble =
            new Border
            {
                Background =
                    isMine
                        ? Brushes.LightBlue
                        : Brushes.LightGray,

                CornerRadius =
                    new CornerRadius(12),

                Padding =
                    new Thickness(10),

                Margin =
                    new Thickness(5, 3, 5, 3),

                MaxWidth = 420,

                HorizontalAlignment =
                    isMine
                        ? HorizontalAlignment.Right
                        : HorizontalAlignment.Left
            };

        StackPanel container =
            new StackPanel();

        TextBlock senderText =
            new TextBlock
            {
                Text =
                    isMine
                        ? "Bạn"
                        : username,

                FontSize = 12,

                FontWeight =
                    FontWeights.Bold,

                Foreground =
                    Brushes.DarkGray,

                Margin =
                    new Thickness(0, 0, 0, 7)
            };

        Border fileCard =
            new Border
            {
                Background =
                    Brushes.White,

                BorderBrush =
                    Brushes.LightGray,

                BorderThickness =
                    new Thickness(1),

                CornerRadius =
                    new CornerRadius(6),

                Padding =
                    new Thickness(10)
            };

        Grid fileGrid =
            new Grid();

        fileGrid.ColumnDefinitions.Add(
            new ColumnDefinition
            {
                Width = GridLength.Auto
            });

        fileGrid.ColumnDefinitions.Add(
            new ColumnDefinition
            {
                Width = new GridLength(
                    1,
                    GridUnitType.Star)
            });

        fileGrid.ColumnDefinitions.Add(
            new ColumnDefinition
            {
                Width = GridLength.Auto
            });


        TextBlock icon =
            new TextBlock
            {
                Text =
                    GetFileIcon(extension),

                FontSize = 30,

                VerticalAlignment =
                    VerticalAlignment.Center,

                Margin =
                    new Thickness(0, 0, 10, 0)
            };

        Grid.SetColumn(
            icon,
            0);

        StackPanel info =
            new StackPanel
            {
                VerticalAlignment =
                    VerticalAlignment.Center
            };

        TextBlock name =
            new TextBlock
            {
                Text =
                    fileName,

                FontWeight =
                    FontWeights.SemiBold,

                FontSize = 13,

                Foreground =
                    Brushes.DarkSlateGray,

                TextWrapping =
                    TextWrapping.Wrap,

                MaxWidth = 250
            };

        TextBlock type =
            new TextBlock
            {
                Text =
                    string.IsNullOrWhiteSpace(
                        extension)
                        ? "Tệp"
                        : $"Tệp {extension}",

                FontSize = 11,

                Foreground =
                    Brushes.Gray,

                Margin =
                    new Thickness(0, 3, 0, 0)
            };

        info.Children.Add(name);
        info.Children.Add(type);

        Grid.SetColumn(
            info,
            1);


        Button downloadButton =
            new Button
            {
                Content = "Tải xuống",

                Width = 82,

                Height = 32,

                Padding =
                    new Thickness(8, 0, 8, 0),

                Background =
                    Brushes.SteelBlue,

                Foreground =
                    Brushes.White,

                BorderThickness =
                    new Thickness(0),

                Cursor =
                    System.Windows.Input.Cursors.Hand,

                VerticalAlignment =
                    VerticalAlignment.Center
            };

        downloadButton.Click +=
            async (s, e) =>
            {
                await DownloadFileAsync(
                    fullUrl,
                    fileName,
                    "Lưu tệp");
            };

        Grid.SetColumn(
            downloadButton,
            2);


        fileGrid.Children.Add(icon);
        fileGrid.Children.Add(info);
        fileGrid.Children.Add(downloadButton);

        fileCard.Child =
            fileGrid;


        container.Children.Add(senderText);
        container.Children.Add(fileCard);

        outerBubble.Child =
            container;

        ChatPanel.Children.Add(
            outerBubble);
    }

    private async Task LoadProtectedImageAsync(Image image, string fullUrl)
    {
        try
        {
            SetAuthorizationHeader();

            using HttpResponseMessage response = await _httpClient.GetAsync(fullUrl);
            if (!response.IsSuccessStatusCode)
            {
                StatusText.Text = "Không thể tải ảnh đính kèm.";
                return;
            }

            byte[] imageBytes = await response.Content.ReadAsByteArrayAsync();
            using var stream = new MemoryStream(imageBytes);

            var bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.StreamSource = stream;
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.EndInit();
            bitmap.Freeze();

            image.Source = bitmap;
        }
        catch (Exception ex)
        {
            StatusText.Text = "Không thể hiển thị ảnh:\n" + ex.Message;
        }
    }

    private async Task PlayVoiceMessageAsync(string fullUrl, string extension)
    {
        try
        {
            _voicePlayer.Stop();
            DeleteTemporaryVoiceFile(_voicePlaybackPath);

            StatusText.Text = "Đang tải tin nhắn thoại...";
            SetAuthorizationHeader();

            using HttpResponseMessage response = await _httpClient.GetAsync(
                fullUrl,
                HttpCompletionOption.ResponseHeadersRead);

            if (!response.IsSuccessStatusCode)
            {
                StatusText.Text = "Không thể tải tin nhắn thoại.";
                return;
            }

            string safeExtension = IsAudioFile(extension) ? extension : ".wav";
            _voicePlaybackPath = Path.Combine(
                Path.GetTempPath(),
                $"mini-discord-play-{Guid.NewGuid():N}{safeExtension}");

            await using (Stream input = await response.Content.ReadAsStreamAsync())
            await using (var output = new FileStream(
                _voicePlaybackPath,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.Read))
            {
                await input.CopyToAsync(output);
            }

            _voicePlayer.Open(new Uri(_voicePlaybackPath, UriKind.Absolute));
            _voicePlayer.Play();
            StatusText.Text = "Đang phát tin nhắn thoại.";
        }
        catch (Exception ex)
        {
            StatusText.Text = "Không thể phát tin nhắn thoại:\n" + ex.Message;
        }
    }

    private async Task DownloadFileAsync(
        string fullUrl,
        string fileName,
        string dialogTitle)
    {
        try
        {
            SaveFileDialog saveDialog =
                new SaveFileDialog
                {
                    FileName =
                        fileName,

                    Title =
                        dialogTitle,

                    Filter =
                        "Tất cả các tệp|*.*"
                };

            if (saveDialog.ShowDialog() != true)
                return;

            StatusText.Text =
                "Đang tải xuống...";

            SetAuthorizationHeader();

            using HttpResponseMessage response =
                await _httpClient.GetAsync(
                    fullUrl,
                    HttpCompletionOption.ResponseHeadersRead);

            if (!response.IsSuccessStatusCode)
            {
                string error =
                    await response.Content
                        .ReadAsStringAsync();

                StatusText.Text =
                    $"Tải file thất bại: {(int)response.StatusCode} {response.ReasonPhrase}\n{error}";

                return;
            }

            await using Stream input =
                await response.Content
                    .ReadAsStreamAsync();

            await using FileStream output =
                new FileStream(
                    saveDialog.FileName,
                    FileMode.Create,
                    FileAccess.Write,
                    FileShare.None);

            await input.CopyToAsync(
                output);

            StatusText.Text =
                $"Đã tải xuống: {fileName}";
        }
        catch (OperationCanceledException)
        {
            StatusText.Text =
                "Đã hủy tải file.";
        }
        catch (Exception ex)
        {
            StatusText.Text =
                "Tải file thất bại:\n" +
                ex.Message;
        }
    }

    private bool IsImageFile(
        string extension)
    {
        return extension == ".jpg"
            || extension == ".jpeg"
            || extension == ".png"
            || extension == ".gif"
            || extension == ".bmp"
            || extension == ".webp";
    }

    private bool IsAudioFile(
        string extension)
    {
        return extension == ".wav"
            || extension == ".mp3"
            || extension == ".m4a"
            || extension == ".aac"
            || extension == ".ogg";
    }

    private string GetFileIcon(
        string extension)
    {
        return extension switch
        {
            ".pdf" => "📕",
            ".doc" => "📘",
            ".docx" => "📘",
            ".xls" => "📗",
            ".xlsx" => "📗",
            ".ppt" => "📙",
            ".pptx" => "📙",
            ".zip" => "🗜️",
            ".rar" => "🗜️",
            ".7z" => "🗜️",
            ".txt" => "📄",
            ".mp3" => "🎵",
            ".wav" => "🎵",
            ".mp4" => "🎬",
            ".mkv" => "🎬",
            _ => "📎"
        };
    }

    private void ScrollChatToBottom()
    {
        Dispatcher.BeginInvoke(
            new Action(
                () =>
                {
                    ChatScrollViewer
                        .ScrollToEnd();
                }));
    }

    private async Task LoadFriendRequestsAsync()
    {
        try
        {
            SetAuthorizationHeader();

            HttpResponseMessage response =
                await _httpClient.GetAsync(
                    $"{ServerConfiguration.ServerUrl}/api/FriendRequests/received");

            if (!response.IsSuccessStatusCode)
                return;

            string result =
                await response.Content
                    .ReadAsStringAsync();

            var requests =
                JsonSerializer.Deserialize<
                    List<FriendRequestResponse>>(
                    result,
                    new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive =
                            true
                    });

            FriendRequestsListBox
                .Items
                .Clear();

            if (requests == null)
                return;

            foreach (var request in requests)
            {
                FriendRequestsListBox
                    .Items
                    .Add(request);
            }
        }
        catch (Exception ex)
        {
            StatusText.Text =
                "Lỗi tải lời mời:\n" +
                ex.Message;
        }
    }

    private async void AcceptFriendButton_Click(
        object sender,
        RoutedEventArgs e)
    {
        if (FriendRequestsListBox.SelectedItem
            is not FriendRequestResponse request)
        {
            StatusText.Text =
                "Hãy chọn một lời mời.";

            return;
        }

        try
        {
            SetAuthorizationHeader();

            HttpResponseMessage response =
                await _httpClient.PostAsync(
                    $"{ServerConfiguration.ServerUrl}/api/FriendRequests/accept/{request.RequestId}",
                    null);

            if (!response.IsSuccessStatusCode)
            {
                StatusText.Text =
                    await response.Content
                        .ReadAsStringAsync();

                return;
            }

            FriendRequestsListBox.Items.Remove(
                request);

            await LoadUsersAsync();

            StatusText.Text =
                $"Đã kết bạn với {request.Username}!";
        }
        catch (Exception ex)
        {
            StatusText.Text =
                "Lỗi chấp nhận lời mời:\n" +
                ex.Message;
        }
    }

    private async void RejectFriendButton_Click(
        object sender,
        RoutedEventArgs e)
    {
        if (FriendRequestsListBox.SelectedItem
            is not FriendRequestResponse request)
        {
            StatusText.Text =
                "Hãy chọn một lời mời.";

            return;
        }

        try
        {
            SetAuthorizationHeader();

            HttpResponseMessage response =
                await _httpClient.PostAsync(
                    $"{ServerConfiguration.ServerUrl}/api/FriendRequests/reject/{request.RequestId}",
                    null);

            if (!response.IsSuccessStatusCode)
            {
                StatusText.Text =
                    await response.Content
                        .ReadAsStringAsync();

                return;
            }

            FriendRequestsListBox.Items.Remove(
                request);

            StatusText.Text =
                $"Đã từ chối lời mời từ {request.Username}.";
        }
        catch (Exception ex)
        {
            StatusText.Text =
                "Lỗi từ chối lời mời:\n" +
                ex.Message;
        }
    }

    private async Task LoadUsersAsync()
    {
        try
        {
            SetAuthorizationHeader();

            HttpResponseMessage response =
                await _httpClient.GetAsync(
                    $"{ServerConfiguration.ServerUrl}/api/Friends");

            if (!response.IsSuccessStatusCode)
                return;

            string result =
                await response.Content
                    .ReadAsStringAsync();

            var users =
                JsonSerializer.Deserialize<
                    List<UserStatusResponse>>(
                    result,
                    new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive =
                            true
                    });

            if (users == null)
                return;

            int? oldSelectedUserId =
                _selectedUserId;

            UsersListBox.Items.Clear();

            foreach (var user in users)
            {
                UsersListBox.Items.Add(user);
            }

            await LoadUnreadCountsAsync();

            if (oldSelectedUserId.HasValue)
            {
                var selectedUser =
                    users.FirstOrDefault(
                        u =>
                            u.Id ==
                            oldSelectedUserId.Value);

                if (selectedUser != null)
                {
                    UsersListBox.SelectedItem =
                        selectedUser;
                }
            }
        }
        catch (Exception ex)
        {
            StatusText.Text =
                "Lỗi tải bạn bè:\n" +
                ex.Message;
        }
    }

    private void SetAuthorizationHeader()
    {
        _httpClient
            .DefaultRequestHeaders
            .Authorization =
                new AuthenticationHeaderValue(
                    "Bearer",
                    Session.Token);
    }

    private void LogoutButton_Click(
        object sender,
        RoutedEventArgs e)
    {
        try
        {
            if (_connection != null)
            {
                _connection.StopAsync();
                _connection.DisposeAsync();
                _connection = null;
            }
        }
        catch
        {
        }

        Session.UserId = 0;
        Session.Username = "";
        Session.Token = "";

        LoginWindow loginWindow =
            new LoginWindow();

        loginWindow.Show();

        Close();
    }
}


public class MessageHistoryResponse
{
    public int SenderId { get; set; }

    public string Username { get; set; } = "";

    public string Message { get; set; } = "";

    public DateTime SentAt { get; set; }

    public bool IsRead { get; set; }

    public string? FileName { get; set; }

    public string? FileUrl { get; set; }
}


public class UnreadCountResponse
{
    public int UserId { get; set; }

    public int Count { get; set; }
}


public class UserStatusResponse
{
    public int Id { get; set; }

    public string Username { get; set; } = "";

    public bool IsOnline { get; set; }

    public int UnreadCount { get; set; }

    public override string ToString()
    {
        string status =
            IsOnline
                ? "🟢 Online"
                : "⚪ Offline";

        string unread =
            UnreadCount > 0
                ? $"   🔴 {UnreadCount}"
                : "";

        return
            $"{Username}    {status}{unread}";
    }
}


public class FriendRequestResponse
{
    public int RequestId { get; set; }

    public int UserId { get; set; }

    public string Username { get; set; } = "";

    public DateTime CreatedAt { get; set; }

    public override string ToString()
    {
        return Username;
    }
}


public class SendFriendRequestRequest
{
    public string Username { get; set; } = "";
}


public class FileUploadResponse
{
    public Guid FileId { get; set; }

    public string FileName { get; set; } = "";
}
