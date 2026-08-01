using Messenger.Core.Models;

namespace Messenger.Core.Services;

/// <summary>
/// Сервис сообщений (базовая реализация без БД - для клиента)
/// </summary>
public class MessageService : IMessageService
{
    public Task<Message> SendMessageAsync(int senderId, MessageType type, string content)
    {
        // В клиентской версии отправка сообщения невозможна без сервера
        throw new InvalidOperationException("Отправка сообщений возможна только через сервер");
    }
    
    public Task<IEnumerable<Message>> GetAllMessagesAsync()
    {
        // В клиентской версии получение сообщений невозможно без сервера
        return Task.FromResult<IEnumerable<Message>>(Enumerable.Empty<Message>());
    }
    
    public Task<OperationResult> DeleteMessageAsync(int messageId)
    {
        // В клиентской версии удаление сообщения невозможно без сервера
        return Task.FromResult(new OperationResult 
        { 
            Success = false, 
            ErrorMessage = "Удаление сообщения возможно только через сервер" 
        });
    }
}
