using Microsoft.Data.Sqlite;
using Messenger.Core.Models;

namespace Messenger.Core.Data;

/// <summary>
/// Репозиторий файловых вложений для работы с SQLite
/// </summary>
public class FileAttachmentRepository : IFileAttachmentRepository
{
    private readonly string _connectionString;
    
    public FileAttachmentRepository(string connectionString)
    {
        _connectionString = connectionString;
        InitializeDatabase();
    }
    
    private void InitializeDatabase()
    {
        using var connection = new SqliteConnection(_connectionString);
        connection.Open();
        
        var command = connection.CreateCommand();
        command.CommandText = @"
            CREATE TABLE IF NOT EXISTS FileAttachments (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                MessageId INTEGER NOT NULL,
                FileName TEXT NOT NULL,
                FilePath TEXT NOT NULL,
                ContentType TEXT NOT NULL,
                FileSize INTEGER NOT NULL,
                UploadedAt TEXT NOT NULL,
                FOREIGN KEY (MessageId) REFERENCES Messages(Id)
            )";
        
        command.ExecuteNonQuery();
    }
    
    public async Task<IEnumerable<FileAttachment>> GetAllAsync()
    {
        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();
        
        var command = connection.CreateCommand();
        command.CommandText = "SELECT * FROM FileAttachments";
        
        var attachments = new List<FileAttachment>();
        using var reader = await command.ExecuteReaderAsync();
        
        while (await reader.ReadAsync())
        {
            attachments.Add(MapToAttachment(reader));
        }
        
        return attachments;
    }
    
    public async Task<FileAttachment?> GetByIdAsync(int id)
    {
        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();
        
        var command = connection.CreateCommand();
        command.CommandText = "SELECT * FROM FileAttachments WHERE Id = @Id";
        command.Parameters.AddWithValue("@Id", id);
        
        using var reader = await command.ExecuteReaderAsync();
        
        if (await reader.ReadAsync())
        {
            return MapToAttachment(reader);
        }
        
        return null;
    }
    
    public async Task<IEnumerable<FileAttachment>> GetByMessageIdAsync(int messageId)
    {
        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();
        
        var command = connection.CreateCommand();
        command.CommandText = "SELECT * FROM FileAttachments WHERE MessageId = @MessageId";
        command.Parameters.AddWithValue("@MessageId", messageId);
        
        var attachments = new List<FileAttachment>();
        using var reader = await command.ExecuteReaderAsync();
        
        while (await reader.ReadAsync())
        {
            attachments.Add(MapToAttachment(reader));
        }
        
        return attachments;
    }
    
    public async Task<FileAttachment> AddAsync(FileAttachment attachment)
    {
        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();
        
        var command = connection.CreateCommand();
        command.CommandText = @"
            INSERT INTO FileAttachments (MessageId, FileName, FilePath, ContentType, FileSize, UploadedAt)
            VALUES (@MessageId, @FileName, @FilePath, @ContentType, @FileSize, @UploadedAt);
            SELECT last_insert_rowid();";
        
        command.Parameters.AddWithValue("@MessageId", attachment.MessageId);
        command.Parameters.AddWithValue("@FileName", attachment.FileName);
        command.Parameters.AddWithValue("@FilePath", attachment.FilePath);
        command.Parameters.AddWithValue("@ContentType", attachment.ContentType);
        command.Parameters.AddWithValue("@FileSize", attachment.FileSize);
        command.Parameters.AddWithValue("@UploadedAt", attachment.UploadedAt.ToString("o"));
        
        var newId = Convert.ToInt32(await command.ExecuteScalarAsync());
        attachment.Id = newId;
        
        return attachment;
    }
    
    public async Task DeleteAsync(int id)
    {
        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();
        
        var command = connection.CreateCommand();
        command.CommandText = "DELETE FROM FileAttachments WHERE Id = @Id";
        command.Parameters.AddWithValue("@Id", id);
        
        await command.ExecuteNonQueryAsync();
    }
    
    private FileAttachment MapToAttachment(SqliteDataReader reader)
    {
        return new FileAttachment
        {
            Id = reader.GetInt32(0),
            MessageId = reader.GetInt32(1),
            FileName = reader.GetString(2),
            FilePath = reader.GetString(3),
            ContentType = reader.GetString(4),
            FileSize = reader.GetInt64(5),
            UploadedAt = DateTime.Parse(reader.GetString(6))
        };
    }
}
