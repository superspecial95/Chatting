using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Windows;

namespace ChatClient;

public partial class LoginWindow : Window
{
    private readonly HttpClient _httpClient;

    public LoginWindow()
    {
        InitializeComponent();

        _httpClient = new HttpClient();
    }

    private async void LoginButton_Click(
        object sender,
        RoutedEventArgs e)
    {
        string username =
            UsernameTextBox.Text.Trim();

        string password =
            PasswordBox.Password;

        if (string.IsNullOrWhiteSpace(username) ||
            string.IsNullOrWhiteSpace(password))
        {
            StatusText.Text =
                "Vui lòng nhập đầy đủ thông tin.";

            return;
        }

        var requestData = new
        {
            username = username,
            password = password
        };

        string json =
            JsonSerializer.Serialize(requestData);

        var content =
            new StringContent(
                json,
                Encoding.UTF8,
                "application/json");

        try
        {
            HttpResponseMessage response =
                await _httpClient.PostAsync(
                    $"{ServerConfiguration.ServerUrl}/api/Auth/login",
                    content);

            string result =
                await response.Content
                    .ReadAsStringAsync();

            if (response.IsSuccessStatusCode)
            {
                var loginResult =
                    JsonSerializer.Deserialize<LoginResponse>(
                        result,
                        new JsonSerializerOptions
                        {
                            PropertyNameCaseInsensitive = true
                        });

                if (loginResult == null)
                {
                    StatusText.Text =
                        "Dữ liệu đăng nhập không hợp lệ.";

                    return;
                }

                Session.UserId =
                    loginResult.UserId;

                Session.Username =
                    loginResult.Username;

                Session.Token =
                    loginResult.Token;

                MainWindow mainWindow =
                    new MainWindow();

                mainWindow.Show();

                Close();
            }
            else
            {
                StatusText.Text =
                    result;
            }
        }
        catch (Exception ex)
        {
            StatusText.Text =
                "Không thể kết nối Server:\n" +
                ex.Message;
        }
    }

    private void RegisterButton_Click(
        object sender,
        RoutedEventArgs e)
    {
        RegisterWindow registerWindow =
            new RegisterWindow();

        registerWindow.Owner = this;

        registerWindow.ShowDialog();
    }
}

public class LoginResponse
{
    public string Message { get; set; } = "";

    public int UserId { get; set; }

    public string Username { get; set; } = "";

    public string Token { get; set; } = "";
}
