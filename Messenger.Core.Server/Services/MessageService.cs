using Messenger.Core.Models;

namespace Messenger.Core.Services;

/// <summary>
/// Сервис сообщений (серверная реализация с БД)
/// </summary>
public class MessageService : IMessageService
{
    private readonly IMessageRepository _messageRepository;
    private readonly IUserRepository _userRepository;
    private readonly INotificationService _notificationService;
    
    public MessageService(
        IMessageRepository messageRepository,
        IUserRepository userRepository,
        INotificationService notificationService)
    {
        _messageRepository = messageRepository;
        _userRepository = userRepository;
        _notificationService = notificationService;
    }
    
    public async Task<Message> SendMessageAsync(int senderId, MessageType type, string content)
    {
        var sender = await _userRepository.GetByIdAsync(senderId);
        if (sender == null)
        {
            throw new InvalidOperationException("Отправитель не найден");
        }
        
        if (sender.Status != UserStatus.Active)
        {
            throw new InvalidOperationException("Пользователь не активен");
        }
        
        var message = new Message
        {
            SenderId = senderId,
            SenderName = sender.Username,
            Type = type,
            Content = content,
            CreatedAt = DateTime.UtcNow,
            IsDeleted = false
        };
        
        var savedMessage = await _messageRepository.AddAsync(message);
        
        // Уведомление о новом сообщении
        await _notificationService.SendNotificationAsync(new Notification
        {
            Type = NotificationType.NewMessage,
            Data = $"Новое сообщение от {sender.Username}",
            CreatedAt = DateTime.UtcNow
        });
        
        return savedMessage;
    }
    
    public async Task<IEnumerable<Message>> GetAllMessagesAsync()
    {
        var messages = await _messageRepository.GetAllAsync();
        return messages.Where(m => !m.IsDeleted);
    }
    
    public async Task<OperationResult> DeleteMessageAsync(int messageId)
    {
        var message = await _messageRepository.GetByIdAsync(messageId);
        if (message == null)
        {
            return new OperationResult 
            { 
                Success = false, 
                ErrorMessage = "Сообщение не найдено" 
            };
        }
        
        await _messageRepository.MarkAsDeletedAsync(messageId);
        
        await _notificationService.SendNotificationAsync(new Notification
        {
            Type = NotificationType.MessageDeleted,
            Data = $"Сообщение удалено",
            CreatedAt = DateTime.UtcNow
        });
        
        return new OperationResult { Success = true };
    }
}
