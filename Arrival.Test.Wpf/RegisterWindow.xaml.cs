using System;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;

namespace Arrival.Test.Wpf
{
    public partial class RegisterWindow : Window
    {
        private readonly string _baseUrl = "http://127.0.0.1:748/api";
        private readonly HttpClient _httpClient;

        public RegisterWindow()
        {
            InitializeComponent();
            _httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(60) };
            _httpClient.DefaultRequestHeaders.ExpectContinue = false;
        }

        #region UI Events

        private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            DragMove();
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }

        private void ShowLoginButton_Click(object sender, RoutedEventArgs e)
        {
            var loginWindow = new LoginWindow();
            loginWindow.Show();
            this.Close();
        }

        private void RegisterUsername_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter && !string.IsNullOrEmpty(RegisterUsernameTextBox.Text))
            {
                RegisterPasswordBox.Focus();
            }
        }

        private void RegisterPassword_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter && !string.IsNullOrEmpty(RegisterPasswordBox.Password))
            {
                RegisterButton_Click(this, new RoutedEventArgs());
            }
        }

        #endregion

        #region Auth Methods

        private async void RegisterButton_Click(object sender, RoutedEventArgs e)
        {
            var username = RegisterUsernameTextBox.Text.Trim();
            var password = RegisterPasswordBox.Password;

            if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password))
            {
                AuthMessageTextBlock.Text = "Имя пользователя и пароль не могут быть пустыми.";
                AuthMessageTextBlock.Foreground = new SolidColorBrush(Color.FromRgb(244, 67, 54));
                return;
            }

            try
            {
                LoadingBorder.Visibility = Visibility.Visible;
                var request = new { username, password };
                var response = await _httpClient.PostAsJsonAsync($"{_baseUrl}/auth/register", request);
                var content = await response.Content.ReadAsStringAsync();

                if (response.IsSuccessStatusCode)
                {
                    AuthMessageTextBlock.Foreground = new SolidColorBrush(Color.FromRgb(76, 175, 80));
                    AuthMessageTextBlock.Text = "✓ Регистрация успешна! Ожидайте одобрения администратора.";
                    RegisterUsernameTextBox.Clear();
                    RegisterPasswordBox.Clear();
                    
                    // Через 2 секунды переключаем на вход
                    await Task.Delay(2000);
                    ShowLoginButton_Click(this, new RoutedEventArgs());
                }
                else
                {
                    var error = ParseErrorMessage(content);
                    AuthMessageTextBlock.Foreground = new SolidColorBrush(Color.FromRgb(244, 67, 54));
                    AuthMessageTextBlock.Text = $"✗ Ошибка: {error}";
                }
            }
            catch (Exception ex)
            {
                AuthMessageTextBlock.Foreground = new SolidColorBrush(Color.FromRgb(244, 67, 54));
                AuthMessageTextBlock.Text = $"✗ Ошибка подключения: {ex.Message}";
            }
            finally
            {
                LoadingBorder.Visibility = Visibility.Collapsed;
            }
        }

        private string ParseErrorMessage(string content)
        {
            try
            {
                var jsonDoc = JsonDocument.Parse(content);
                if (jsonDoc.RootElement.TryGetProperty("message", out var messageElem))
                    return messageElem.GetString() ?? "Неизвестная ошибка";
                if (jsonDoc.RootElement.TryGetProperty("error", out var errorElem))
                    return errorElem.GetString() ?? "Неизвестная ошибка";
                if (jsonDoc.RootElement.TryGetProperty("title", out var titleElem))
                    return titleElem.GetString() ?? "Неизвестная ошибка";
            }
            catch
            {
                // Если не удалось распарсить JSON
            }
            return content.Length > 100 ? content.Substring(0, 100) + "..." : content;
        }

        #endregion
    }
}
