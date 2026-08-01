using Messenger.Core.Models;
using Messenger.Core.Interfaces;
using Messenger.Core.Utils;

namespace Messenger.Core.Services;

/// <summary>
/// Сервис сообщений (серверная реализация с БД)
/// </summary>
public class MessageService : IMessageService
{
    private readonly IMessageRepository _messageRepository;
    private readonly IFileAttachmentRepository _fileAttachmentRepository;
    private readonly IUserRepository _userRepository;
    private readonly INotificationService _notificationService;
    
    public MessageService(
        IMessageRepository messageRepository,
        IUserRepository userRepository,
        IFileAttachmentRepository fileAttachmentRepository,
        INotificationService notificationService)
    {
        _messageRepository = messageRepository;
        _userRepository = userRepository;
        _fileAttachmentRepository = fileAttachmentRepository;
        _notificationService = notificationService;
    }
    
    public async Task<Message> SendMessageAsync(int senderId, MessageType type, string content, int? recipientId = null)
    {
        var sender = await _userRepository.GetByIdAsync(senderId);
        if (sender == null)
        {
            ConsoleLogger.Error($"SendMessage failed: sender with ID {senderId} not found");
            throw new InvalidOperationException("Отправитель не найден");
        }
        
        if (sender.Status != UserStatus.Active)
        {
            ConsoleLogger.Warn($"SendMessage failed: sender '{sender.Username}' is not active (status: {sender.Status})");
            throw new InvalidOperationException("Пользователь не активен");
        }
        
        // Если указан получатель, проверяем его существование
        if (recipientId.HasValue)
        {
            var recipient = await _userRepository.GetByIdAsync(recipientId.Value);
            if (recipient == null)
            {
                ConsoleLogger.Error($"SendMessage failed: recipient with ID {recipientId.Value} not found");
                throw new InvalidOperationException("Получатель не найден");
            }
        }
        
        var message = new Message
        {
            SenderId = senderId,
            SenderName = sender.Username,
            RecipientId = recipientId,
            Type = type,
            Content = content,
            CreatedAt = DateTime.UtcNow,
            IsDeleted = false
        };
        
        var savedMessage = await _messageRepository.AddAsync(message);
        ConsoleLogger.Info($"Message #{savedMessage.Id} from '{sender.Username}' saved to database");
        
        // Уведомление о новом сообщении через WebSocket
        if (recipientId.HasValue)
        {
            // Личное сообщение - отправляем через WebSocket получателю
            if (_notificationService is NotificationService ns)
            {
                await ns.SendMessageToUserAsync(recipientId.Value, content, senderId, sender.Username);
            }
            else
            {
                await _notificationService.SendNotificationToUserAsync(recipientId.Value, new Notification
                {
                    Type = NotificationType.NewMessage,
                    Data = $"Новое личное сообщение от {sender.Username}",
                    CreatedAt = DateTime.UtcNow
                });
            }
        }
        else
        {
            // Общее сообщение - уведомляем всех
            await _notificationService.SendNotificationAsync(new Notification
            {
                Type = NotificationType.NewMessage,
                Data = $"Новое сообщение от {sender.Username}",
                CreatedAt = DateTime.UtcNow
            });
        }
        
        return savedMessage;
    }
    
    public async Task<IEnumerable<Message>> GetAllMessagesAsync()
    {
        var messages = await _messageRepository.GetAllAsync();
        var result = messages.Where(m => !m.IsDeleted).ToList();
        ConsoleLogger.Info($"GetAllMessages returned {result.Count} messages");
        return result;
    }
    
    public async Task<IEnumerable<Message>> GetMessagesForUserAsync(int userId)
    {
        var messages = await _messageRepository.GetByRecipientIdAsync(userId);
        var result = messages.Where(m => !m.IsDeleted).ToList();
        ConsoleLogger.Info($"GetMessagesForUser({userId}) returned {result.Count} messages");
        return result;
    }
    
    public async Task<OperationResult> DeleteMessageAsync(int messageId)
    {
        var message = await _messageRepository.GetByIdAsync(messageId);
        if (message == null)
        {
            ConsoleLogger.Warn($"DeleteMessage failed: message with ID {messageId} not found");
            return new OperationResult 
            { 
                Success = false, 
                ErrorMessage = "Сообщение не найдено" 
            };
        }
        
        await _messageRepository.MarkAsDeletedAsync(messageId);
        ConsoleLogger.Info($"Message #{messageId} marked as deleted");
        
        await _notificationService.SendNotificationAsync(new Notification
        {
            Type = NotificationType.MessageDeleted,
            Data = $"Сообщение удалено",
            CreatedAt = DateTime.UtcNow
        });
        
        return new OperationResult { Success = true };
    }
}
