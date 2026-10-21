using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Windows;

namespace ChatClient;

public partial class RegisterWindow : Window
{
    private readonly HttpClient _httpClient =
        new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(10)
        };

    public RegisterWindow()
    {
        InitializeComponent();
    }

    private async void RegisterButton_Click(
        object sender,
        RoutedEventArgs e)
    {
        string username =
            UsernameTextBox.Text.Trim();

        string email =
            EmailTextBox.Text.Trim();

        string password =
            PasswordBox.Password;

        string confirmPassword =
            ConfirmPasswordBox.Password;

        // =========================
        // KIỂM TRA USERNAME
        // =========================

        if (string.IsNullOrWhiteSpace(username))
        {
            StatusText.Text =
                "❌ Vui lòng nhập username.";

            UsernameTextBox.Focus();

            return;
        }

        if (username.Length < 3)
        {
            StatusText.Text =
                "❌ Username phải có ít nhất 3 ký tự.";

            UsernameTextBox.Focus();

            return;
        }

        if (username.Length > 20)
        {
            StatusText.Text =
                "❌ Username không được quá 20 ký tự.";

            UsernameTextBox.Focus();

            return;
        }

        if (!Regex.IsMatch(
                username,
                @"^[a-zA-Z0-9_]+$"))
        {
            StatusText.Text =
                "❌ Username chỉ được dùng chữ, số và dấu _.";

            UsernameTextBox.Focus();

            return;
        }

        // =========================
        // KIỂM TRA EMAIL
        // =========================

        if (string.IsNullOrWhiteSpace(email))
        {
            StatusText.Text =
                "❌ Vui lòng nhập email.";

            EmailTextBox.Focus();

            return;
        }

        if (!IsValidEmail(email))
        {
            StatusText.Text =
                "❌ Email không hợp lệ.\nVí dụ: abc@gmail.com";

            EmailTextBox.Focus();

            return;
        }

        // =========================
        // KIỂM TRA PASSWORD
        // =========================

        if (string.IsNullOrWhiteSpace(password))
        {
            StatusText.Text =
                "❌ Vui lòng nhập password.";

            PasswordBox.Focus();

            return;
        }

        if (password.Length < 8)
        {
            StatusText.Text =
                "❌ Password phải có ít nhất 8 ký tự.";

            PasswordBox.Focus();

            return;
        }

        if (password.Length > 100)
        {
            StatusText.Text =
                "❌ Password không được quá 100 ký tự.";

            PasswordBox.Focus();

            return;
        }

        // =========================
        // KIỂM TRA PASSWORD LẦN 2
        // =========================

        if (string.IsNullOrWhiteSpace(confirmPassword))
        {
            StatusText.Text =
                "❌ Vui lòng nhập lại password.";

            ConfirmPasswordBox.Focus();

            return;
        }

        if (password != confirmPassword)
        {
            StatusText.Text =
                "❌ Hai password không giống nhau.";

            ConfirmPasswordBox.Focus();

            return;
        }

        // =========================
        // GỬI REQUEST
        // =========================

        var requestData = new
        {
            username = username,
            email = email,
            password = password
        };

        string json =
            JsonSerializer.Serialize(requestData);

        using var content =
            new StringContent(
                json,
                Encoding.UTF8,
                "application/json");

        try
        {
            StatusText.Text =
                "Đang đăng ký...";

            RegisterButton.IsEnabled = false;

            HttpResponseMessage response =
                await _httpClient.PostAsync(
                    $"{ServerConfiguration.ServerUrl}/api/Auth/register",
                    content);

            string result =
                await response.Content
                    .ReadAsStringAsync();

            if (response.IsSuccessStatusCode)
            {
                MessageBox.Show(
                    "Đăng ký tài khoản thành công!",
                    "Thành công",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);

                Close();

                return;
            }

            // Server trả lỗi
            StatusText.Text =
                GetServerErrorMessage(result);
        }
        catch (TaskCanceledException)
        {
            StatusText.Text =
                "❌ Server phản hồi quá lâu.\n" +
                "Hãy kiểm tra ChatServer đang chạy.";

        }
        catch (HttpRequestException ex)
        {
            StatusText.Text =
                "❌ Không thể kết nối Server.\n" +
                ex.Message;
        }
        catch (Exception ex)
        {
            StatusText.Text =
                "❌ Đăng ký thất bại.\n" +
                ex.Message;
        }
        finally
        {
            RegisterButton.IsEnabled = true;
        }
    }

    private bool IsValidEmail(
        string email)
    {
        try
        {
            return Regex.IsMatch(
                email,
                @"^[^@\s]+@[^@\s]+\.[^@\s]+$");
        }
        catch
        {
            return false;
        }
    }

    private string GetServerErrorMessage(
        string result)
    {
        if (string.IsNullOrWhiteSpace(result))
        {
            return "❌ Đăng ký thất bại.";
        }

        string message =
            result.Trim();

        // Một số response có dạng JSON
        try
        {
            using JsonDocument document =
                JsonDocument.Parse(result);

            if (document.RootElement.TryGetProperty(
                    "message",
                    out JsonElement messageElement))
            {
                string? serverMessage =
                    messageElement.GetString();

                if (!string.IsNullOrWhiteSpace(
                        serverMessage))
                {
                    message =
                        serverMessage;
                }
            }
        }
        catch
        {
            // Response không phải JSON,
            // dùng nguyên nội dung server trả về.
        }

        return $"❌ {message}";
    }

    private void BackButton_Click(
        object sender,
        RoutedEventArgs e)
    {
        Close();
    }
}
