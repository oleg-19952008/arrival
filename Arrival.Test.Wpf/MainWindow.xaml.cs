using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using Microsoft.AspNetCore.SignalR.Client;
using Brush = System.Windows.Media.Brush;
using Brushes = System.Windows.Media.Brushes;
using Application = System.Windows.Application;
using MessageBox = System.Windows.MessageBox;
using KeyEventArgs = System.Windows.Input.KeyEventArgs;
using HorizontalAlignment = System.Windows.HorizontalAlignment;

namespace Arrival.Test.Wpf
{
    public partial class MainWindow : Window
    {
        private readonly string _baseUrl = "http://89.109.34.250:748/api";
        private readonly string _signalRUrl = "http://89.109.34.250:748/notificationHub";
        
        private readonly HttpClient _httpClient;
        private string? _token;
        private int? _currentUserId;
        private string? _currentUsername;
        private HubConnection? _hubConnection;
        private bool _isConnectedToHub;
        private DispatcherTimer? _messagesPollingTimer;
        
        // Текущий выбранный чат
        private ChatItem? _selectedChat;
        private ObservableCollection<ChatItem> _chatItems = new();
        private ObservableCollection<MessageViewModel> _messages = new();
        
        // Все пользователи для фильтрации
        private List<UserInfo> _allUsers = new();

        public MainWindow()
        {
            InitializeComponent();
            
            _httpClient = new HttpClient
            {
                Timeout = TimeSpan.FromSeconds(60)
            };
            _httpClient.DefaultRequestHeaders.ExpectContinue = false;
            
            // Инициализация списка чатов
            ChatListBox.ItemsSource = _chatItems;
            MessagesItemsControl.ItemsSource = _messages;
            
            // Таймер опроса сообщений
            _messagesPollingTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(3)
            };
            _messagesPollingTimer.Tick += async (s, e) => await LoadMessagesForCurrentChat();
        }

        #region Auth Methods

        private async void RegisterButton_Click(object sender, RoutedEventArgs e)
        {
            var username = RegisterUsernameTextBox.Text.Trim();
            var password = RegisterPasswordBox.Password;

            if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password))
            {
                AuthMessageTextBlock.Text = "Имя пользователя и пароль не могут быть пустыми.";
                return;
            }

            try
            {
                var request = new { username, password };
                var response = await _httpClient.PostAsJsonAsync($"{_baseUrl}/auth/register", request);
                var content = await response.Content.ReadAsStringAsync();

                if (response.IsSuccessStatusCode)
                {
                    AuthMessageTextBlock.Foreground = Brushes.LightGreen;
                    AuthMessageTextBlock.Text = "✓ Регистрация успешна! Ожидайте одобрения администратора.";
                    RegisterUsernameTextBox.Clear();
                    RegisterPasswordBox.Clear();
                }
                else
                {
                    var error = ParseErrorMessage(content);
                    AuthMessageTextBlock.Foreground = Brushes.LightCoral;
                    AuthMessageTextBlock.Text = $"✗ Ошибка: {error}";
                }
            }
            catch (Exception ex)
            {
                AuthMessageTextBlock.Foreground = Brushes.LightCoral;
                AuthMessageTextBlock.Text = $"✗ Ошибка подключения: {ex.Message}";
            }
        }

        private void ShowRegisterButton_Click(object sender, RoutedEventArgs e)
        {
            LoginPanel.Visibility = Visibility.Collapsed;
            RegisterPanel.Visibility = Visibility.Visible;
        }

        private void ShowLoginButton_Click(object sender, RoutedEventArgs e)
        {
            LoginPanel.Visibility = Visibility.Visible;
            RegisterPanel.Visibility = Visibility.Collapsed;
        }

        private async void LoginButton_Click(object sender, RoutedEventArgs e)
        {
            var username = LoginUsernameTextBox.Text.Trim();
            var password = LoginPasswordBox.Password;

            if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password))
            {
                LoginMessageTextBlock.Text = "Имя пользователя и пароль не могут быть пустыми.";
                return;
            }

            try
            {
                var request = new { username, password };
                var response = await _httpClient.PostAsJsonAsync($"{_baseUrl}/auth/login", request);
                var content = await response.Content.ReadAsStringAsync();

                if (response.IsSuccessStatusCode)
                {
                    var jsonDoc = JsonDocument.Parse(content);
                    _token = jsonDoc.RootElement.GetProperty("token").GetString();
                    _currentUserId = jsonDoc.RootElement.GetProperty("userId").GetInt32();
                    _currentUsername = jsonDoc.RootElement.GetProperty("username").GetString();
                    var role = jsonDoc.RootElement.GetProperty("role").GetString();
                    var status = jsonDoc.RootElement.GetProperty("status").GetString();

                    _httpClient.DefaultRequestHeaders.Authorization = 
                        new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _token);

                    LoginMessageTextBlock.Text = "";
                    
                    // Скрываем экран авторизации
                    AuthOverlay.Visibility = Visibility.Collapsed;
                    
                    // Обновляем UI
                    UpdateLoggedInUI(_currentUsername, role, status);
                    
                    // Загружаем список пользователей
                    await LoadUsers();
                    
                    // Подключаемся к WebSocket
                    await ConnectToWebSocket();
                }
                else
                {
                    var error = ParseErrorMessage(content);
                    LoginMessageTextBlock.Foreground = Brushes.LightCoral;
                    LoginMessageTextBlock.Text = $"✗ Ошибка входа: {error}";
                }
            }
            catch (Exception ex)
            {
                LoginMessageTextBlock.Foreground = Brushes.LightCoral;
                LoginMessageTextBlock.Text = $"✗ Ошибка подключения: {ex.Message}";
            }
        }

        private void UpdateLoggedInUI(string username, string role, string status)
        {
            CurrentUserTextBlock.Text = $"{username} | {role}";
            LogoutButton.Visibility = Visibility.Visible;
            
            // Запускаем таймер
            _messagesPollingTimer?.Start();
        }

        #endregion

        #region Users & Chats

        private async void RefreshUsersButton_Click(object sender, RoutedEventArgs e)
        {
            await LoadUsers();
        }

        private async Task LoadUsers()
        {
            try
            {
                var response = await _httpClient.GetAsync($"{_baseUrl}/users");
                if (response.IsSuccessStatusCode)
                {
                    var content = await response.Content.ReadAsStringAsync();
                    var users = JsonDocument.Parse(content).RootElement;

                    _allUsers.Clear();
                    foreach (var user in users.EnumerateArray())
                    {
                        var id = user.GetProperty("id").GetInt32();
                        var uname = user.GetProperty("username").GetString();
                        var userStatus = user.GetProperty("status").GetString();
                        
                        // Не добавляем текущего пользователя в список чатов
                        if (id != _currentUserId)
                        {
                            _allUsers.Add(new UserInfo
                            {
                                Id = id,
                                Username = uname ?? "",
                                Status = userStatus ?? "Offline"
                            });
                        }
                    }
                    
                    // Создаем чат "Общий" первым
                    _chatItems.Clear();
                    _chatItems.Add(new ChatItem
                    {
                        Id = 0,
                        Username = "Общий чат",
                        AvatarText = "📢",
                        IsOnline = true,
                        LastMessagePreview = "Нажмите для просмотра общих сообщений",
                        LastTime = ""
                    });
                    
                    // Добавляем личные чаты
                    foreach (var user in _allUsers)
                    {
                        _chatItems.Add(new ChatItem
                        {
                            Id = user.Id,
                            Username = user.Username,
                            AvatarText = GetAvatarText(user.Username),
                            IsOnline = user.Status == "Online",
                            LastMessagePreview = "Нет сообщений",
                            LastTime = ""
                        });
                    }
                    
                    ChatListBox.Items.Refresh();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка загрузки пользователей: {ex.Message}", "Ошибка", 
                    MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void SearchTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            var searchText = SearchTextBox.Text.ToLower().Trim();
            
            var view = CollectionViewSource.GetDefaultView(ChatListBox.ItemsSource);
            if (view != null)
            {
                view.Filter = item =>
                {
                    if (item is ChatItem chat)
                    {
                        return string.IsNullOrEmpty(searchText) || 
                               chat.Username.ToLower().Contains(searchText);
                    }
                    return true;
                };
            }
        }

        private async void ChatListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ChatListBox.SelectedItem is ChatItem selectedChat)
            {
                _selectedChat = selectedChat;
                
                // Обновляем заголовок чата
                CurrentChatNameText.Text = selectedChat.Username;
                CurrentChatAvatarText.Text = selectedChat.AvatarText;
                CurrentChatStatusText.Text = selectedChat.IsOnline ? "Онлайн" : "Офлайн";
                
                // Загружаем сообщения для выбранного чата
                await LoadMessagesForCurrentChat();
            }
        }

        #endregion

        #region Messages

        private async void SendButton_Click(object sender, RoutedEventArgs e)
        {
            await SendMessage();
        }

        private async void MessageInputTextBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                await SendMessage();
            }
        }

        private async Task SendMessage()
        {
            var content = MessageInputTextBox.Text.Trim();
            if (string.IsNullOrEmpty(content))
                return;

            if (_selectedChat == null)
            {
                MessageBox.Show("Выберите чат для отправки сообщения.", "Предупреждение", 
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                int? recipientId = null;
                if (_selectedChat.Id != 0) // Не общий чат
                {
                    recipientId = _selectedChat.Id;
                }

                var request = new
                {
                    content,
                    type = "text",
                    recipientId,
                    recipientLogin = recipientId.HasValue ? _selectedChat.Username : null
                };

                var jsonRequest = JsonSerializer.Serialize(request);
                var stringContent = new StringContent(jsonRequest, Encoding.UTF8, "application/json");

                var sendResponse = await _httpClient.PostAsync($"{_baseUrl}/messages", stringContent);
                
                if (sendResponse.IsSuccessStatusCode)
                {
                    MessageInputTextBox.Clear();
                    await LoadMessagesForCurrentChat();
                }
                else
                {
                    var errorContent = await sendResponse.Content.ReadAsStringAsync();
                    var error = ParseErrorMessage(errorContent);
                    MessageBox.Show($"Ошибка отправки: {error}", "Ошибка", 
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка: {ex.Message}", "Ошибка", 
                    MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private async Task LoadMessagesForCurrentChat()
        {
            if (_selectedChat == null)
                return;

            try
            {
                string endpoint;
                if (_selectedChat.Id == 0) // Общий чат
                {
                    endpoint = $"{_baseUrl}/messages/my";
                }
                else // Личный чат
                {
                    endpoint = $"{_baseUrl}/messages/my?recipientId={_selectedChat.Id}";
                }

                var response = await _httpClient.GetAsync(endpoint);
                if (response.IsSuccessStatusCode)
                {
                    var content = await response.Content.ReadAsStringAsync();
                    var messagesDoc = JsonDocument.Parse(content);
                    var messagesArray = messagesDoc.RootElement.EnumerateArray().ToList();

                    _messages.Clear();
                    
                    foreach (var msg in messagesArray)
                    {
                        var id = msg.GetProperty("id").GetInt32();
                        var senderId = msg.GetProperty("senderId").GetInt32();
                        var senderName = msg.GetProperty("senderName").GetString();
                        var textContent = msg.GetProperty("content").GetString();
                        var createdAt = msg.GetProperty("createdAt").GetDateTime().ToLocalTime();

                        bool isMyMessage = senderId == _currentUserId;
                        
                        _messages.Add(new MessageViewModel
                        {
                            Id = id,
                            SenderId = senderId,
                            SenderName = isMyMessage ? "Вы" : (senderName ?? "Неизвестный"),
                            Content = textContent ?? "",
                            Time = createdAt,
                            IsMyMessage = isMyMessage,
                            Alignment = isMyMessage ? HorizontalAlignment.Right : HorizontalAlignment.Left,
                            TimeAlignment = isMyMessage ? HorizontalAlignment.Right : HorizontalAlignment.Left,
                            MessageBackground = isMyMessage ? 
                                Application.Current.Resources["MyMessageBackground"] as Brush : 
                                Application.Current.Resources["OtherMessageBackground"] as Brush,
                            ShowSender = Visibility.Collapsed // В личных чатах имя не показываем
                        });
                    }
                    
                    // Прокрутка вниз
                    MessagesScrollViewer.ScrollToEnd();
                    
                    // Обновляем превью последнего сообщения в списке чатов
                    if (messagesArray.Count > 0 && _selectedChat != null)
                    {
                        var lastMsg = messagesArray[^1];
                        var lastContent = lastMsg.GetProperty("content").GetString();
                        var lastTime = lastMsg.GetProperty("createdAt").GetDateTime().ToLocalTime();
                        
                        _selectedChat.LastMessagePreview = lastContent?.Length > 30 
                            ? lastContent[..30] + "..." 
                            : lastContent;
                        _selectedChat.LastTime = lastTime.ToString("HH:mm");
                        
                        ChatListBox.Items.Refresh();
                    }
                }
            }
            catch (Exception ex)
            {
                // Тихо игнорируем ошибки при автообновлении
                System.Diagnostics.Debug.WriteLine($"Ошибка загрузки сообщений: {ex.Message}");
            }
        }

        #endregion

        #region WebSocket

        private async void WebSocketConnectButton_Click(object sender, RoutedEventArgs e)
        {
            await ConnectToWebSocket();
        }

        private async void WebSocketDisconnectButton_Click(object sender, RoutedEventArgs e)
        {
            await DisconnectFromWebSocket();
        }

        private async Task ConnectToWebSocket()
        {
            if (_isConnectedToHub || _currentUserId == null)
                return;

            try
            {
                WebSocketConnectButton.Visibility = Visibility.Collapsed;
                WebSocketDisconnectButton.Visibility = Visibility.Visible;

                _hubConnection = new HubConnectionBuilder()
                    .WithUrl($"{_signalRUrl}?userId={_currentUserId}&token={_token}")
                    .WithAutomaticReconnect()
                    .Build();

                _hubConnection.On<object>("message", (messageData) =>
                {
                    Dispatcher.Invoke(async () =>
                    {
                        try
                        {
                            var json = JsonSerializer.Serialize(messageData);
                            using var doc = JsonDocument.Parse(json);
                            var root = doc.RootElement;

                            var senderName = root.TryGetProperty("senderName", out var snEl) 
                                ? snEl.GetString() : "Неизвестный";
                            var text = root.TryGetProperty("text", out var tEl) 
                                ? tEl.GetString() : "";

                            // Просто выводим в debug, уведомления убраны
                            System.Diagnostics.Debug.WriteLine($"Новое сообщение от {senderName}: {text}");
                            
                            // Обновляем сообщения
                            await LoadMessagesForCurrentChat();
                        }
                        catch (Exception ex)
                        {
                            System.Diagnostics.Debug.WriteLine($"Ошибка обработки сообщения: {ex.Message}");
                        }
                    });
                });

                await _hubConnection.StartAsync();
                _isConnectedToHub = true;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка подключения к уведомлениям: {ex.Message}", 
                    "Предупреждение", MessageBoxButton.OK, MessageBoxImage.Warning);
                WebSocketConnectButton.Visibility = Visibility.Visible;
                WebSocketDisconnectButton.Visibility = Visibility.Collapsed;
            }
        }

        private async Task DisconnectFromWebSocket()
        {
            if (!_isConnectedToHub || _hubConnection == null)
                return;

            try
            {
                await _hubConnection.StopAsync();
                await _hubConnection.DisposeAsync();
                _hubConnection = null;
                _isConnectedToHub = false;

                WebSocketConnectButton.Visibility = Visibility.Visible;
                WebSocketDisconnectButton.Visibility = Visibility.Collapsed;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Ошибка отключения: {ex.Message}");
            }
        }

        #endregion

        #region Logout

        private async void LogoutButton_Click(object sender, RoutedEventArgs e)
        {
            await DisconnectFromWebSocket();

            try
            {
                await _httpClient.PostAsync($"{_baseUrl}/auth/logout", null);
            }
            catch { /* Игнорируем ошибки выхода */ }
            finally
            {
                _token = null;
                _currentUserId = null;
                _currentUsername = null;
                _httpClient.DefaultRequestHeaders.Authorization = null;
                
                _chatItems.Clear();
                _messages.Clear();
                _allUsers.Clear();
                _selectedChat = null;
                
                CurrentUserTextBlock.Text = "";
                CurrentChatNameText.Text = "";
                CurrentChatAvatarText.Text = "";
                CurrentChatStatusText.Text = "";
                
                AuthOverlay.Visibility = Visibility.Visible;
                LogoutButton.Visibility = Visibility.Collapsed;
                
                _messagesPollingTimer?.Stop();
            }
        }

        #endregion

        #region Helpers

        private string GetAvatarText(string username)
        {
            if (string.IsNullOrEmpty(username))
                return "?";
            
            // Берем первые две буквы или первую букву + символ
            var letters = username.Take(2).Select(char.ToUpper).ToArray();
            return letters.Length > 0 ? new string(letters) : "?";
        }

        private string ParseErrorMessage(string json)
        {
            try
            {
                var doc = JsonDocument.Parse(json);
                if (doc.RootElement.TryGetProperty("message", out var messageProp))
                {
                    return messageProp.GetString() ?? "Неизвестная ошибка";
                }
            }
            catch { }
            return "Неизвестная ошибка";
        }

        #endregion
    }

    #region Models

    public class ChatItem
    {
        public int Id { get; set; }
        public string Username { get; set; } = "";
        public string AvatarText { get; set; } = "";
        public bool IsOnline { get; set; }
        public string LastMessagePreview { get; set; } = "";
        public string LastTime { get; set; } = "";
    }

    public class UserInfo
    {
        public int Id { get; set; }
        public string Username { get; set; } = "";
        public string Status { get; set; } = "";
    }

    public class MessageViewModel
    {
        public int Id { get; set; }
        public int SenderId { get; set; }
        public string SenderName { get; set; } = "";
        public string Content { get; set; } = "";
        public DateTime Time { get; set; }
        public bool IsMyMessage { get; set; }
        public HorizontalAlignment Alignment { get; set; }
        public HorizontalAlignment TimeAlignment { get; set; }
        public Brush? MessageBackground { get; set; }
        public Visibility ShowSender { get; set; } = Visibility.Collapsed;
        
        public string TimeText => Time.ToString("HH:mm");
    }

    #endregion
}
