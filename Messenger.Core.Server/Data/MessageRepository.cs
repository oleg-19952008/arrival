using Microsoft.Data.Sqlite;
using Messenger.Core.Models;
using Messenger.Core.Interfaces;
namespace Messenger.Core.Data;

/// <summary>
/// Репозиторий сообщений для работы с SQLite
/// </summary>
public class MessageRepository : IMessageRepository
{
    private readonly string _connectionString;
    private bool _initialized = false;

    public MessageRepository(string connectionString)
    {
        _connectionString = connectionString;
    }

    private void EnsureDatabaseInitialized()
    {
        if (_initialized) return;
        
        using var connection = new SqliteConnection(_connectionString);
        connection.Open();

        var command = connection.CreateCommand();
        command.CommandText = @"
            CREATE TABLE IF NOT EXISTS Messages (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                SenderId INTEGER NOT NULL,
                SenderName TEXT NOT NULL,
                RecipientId INTEGER,
                Type INTEGER NOT NULL,
                Content TEXT NOT NULL,
                CreatedAt TEXT NOT NULL,
                IsDeleted INTEGER NOT NULL DEFAULT 0
            )";

        command.ExecuteNonQuery();
        _initialized = true;
    }

    public async Task<IEnumerable<Message>> GetAllAsync()
    {
        EnsureDatabaseInitialized();
        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();

        var command = connection.CreateCommand();
        command.CommandText = "SELECT * FROM Messages ORDER BY CreatedAt ASC";

        var messages = new List<Message>();
        using var reader = await command.ExecuteReaderAsync();

        while (await reader.ReadAsync())
        {
            messages.Add(MapToMessage(reader));
        }

        return messages;
    }

    public async Task<IEnumerable<Message>> GetByRecipientIdAsync(int recipientId)
    {
        EnsureDatabaseInitialized();
        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();

        var command = connection.CreateCommand();
        command.CommandText = "SELECT * FROM Messages WHERE RecipientId = @RecipientId OR RecipientId IS NULL ORDER BY CreatedAt ASC";
        command.Parameters.AddWithValue("@RecipientId", recipientId);

        var messages = new List<Message>();
        using var reader = await command.ExecuteReaderAsync();

        while (await reader.ReadAsync())
        {
            messages.Add(MapToMessage(reader));
        }

        return messages;
    }

    public async Task<Message?> GetByIdAsync(int id)
    {
        EnsureDatabaseInitialized();
        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();

        var command = connection.CreateCommand();
        command.CommandText = "SELECT * FROM Messages WHERE Id = @Id";
        command.Parameters.AddWithValue("@Id", id);

        using var reader = await command.ExecuteReaderAsync();

        if (await reader.ReadAsync())
        {
            return MapToMessage(reader);
        }

        return null;
    }

    public async Task<Message> AddAsync(Message message)
    {
        EnsureDatabaseInitialized();
        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();

        var command = connection.CreateCommand();
        command.CommandText = @"
            INSERT INTO Messages (SenderId, SenderName, RecipientId, Type, Content, CreatedAt, IsDeleted)
            VALUES (@SenderId, @SenderName, @RecipientId, @Type, @Content, @CreatedAt, @IsDeleted);
            SELECT last_insert_rowid();";

        command.Parameters.AddWithValue("@SenderId", message.SenderId);
        command.Parameters.AddWithValue("@SenderName", message.SenderName ?? string.Empty);
        command.Parameters.AddWithValue("@RecipientId", message.RecipientId ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("@Type", (int)message.Type);
        command.Parameters.AddWithValue("@Content", message.Content);
        command.Parameters.AddWithValue("@CreatedAt", message.CreatedAt.ToString("o"));
        command.Parameters.AddWithValue("@IsDeleted", message.IsDeleted ? 1 : 0);

        var newId = Convert.ToInt32(await command.ExecuteScalarAsync());
        message.Id = newId;

        return message;
    }

    public async Task DeleteAsync(int id)
    {
        EnsureDatabaseInitialized();
        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();

        var command = connection.CreateCommand();
        command.CommandText = "DELETE FROM Messages WHERE Id = @Id";
        command.Parameters.AddWithValue("@Id", id);

        await command.ExecuteNonQueryAsync();
    }

    public async Task MarkAsDeletedAsync(int id)
    {
        EnsureDatabaseInitialized();
        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();

        var command = connection.CreateCommand();
        command.CommandText = "UPDATE Messages SET IsDeleted = 1 WHERE Id = @Id";
        command.Parameters.AddWithValue("@Id", id);

        await command.ExecuteNonQueryAsync();
    }

    private Message MapToMessage(SqliteDataReader reader)
    {
        int offset = reader.IsDBNull(3) ? 0 : 1;
        return new Message
        {
            Id = reader.GetInt32(0),
            SenderId = reader.GetInt32(1),
            SenderName = reader.GetString(2),
            RecipientId = reader.IsDBNull(3) ? null : reader.GetInt32(3),
            Type = (MessageType)reader.GetInt32(3 + offset),
            Content = reader.GetString(4 + offset),
            CreatedAt = DateTime.Parse(reader.GetString(5 + offset)),
            IsDeleted = reader.GetInt32(6 + offset) == 1
        };
    }
}
