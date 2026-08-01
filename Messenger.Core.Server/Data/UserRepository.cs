using Microsoft.Data.Sqlite;
using Microsoft.Extensions;
using Microsoft.Extensions.DependencyInjection; // <--- Добавить это
using Messenger.Core.Models;
namespace Messenger.Core.Data;

/// <summary>
/// Репозиторий пользователей для работы с SQLite
/// </summary>
public class UserRepository : IUserRepository
{
    private readonly string _connectionString;
    
    public UserRepository(string connectionString)
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
            CREATE TABLE IF NOT EXISTS Users (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                Username TEXT UNIQUE NOT NULL,
                PasswordHash TEXT NOT NULL,
                Role INTEGER NOT NULL,
                Status INTEGER NOT NULL,
                CreatedAt TEXT NOT NULL,
                LastLoginAt TEXT
            )";
        
        command.ExecuteNonQuery();
    }
    
    public async Task<IEnumerable<User>> GetAllAsync()
    {
        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();
        
        var command = connection.CreateCommand();
        command.CommandText = "SELECT * FROM Users";
        
        var users = new List<User>();
        using var reader = await command.ExecuteReaderAsync();
        
        while (await reader.ReadAsync())
        {
            users.Add(MapToUser(reader));
        }
        
        return users;
    }
    
    public async Task<User?> GetByIdAsync(int id)
    {
        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();
        
        var command = connection.CreateCommand();
        command.CommandText = "SELECT * FROM Users WHERE Id = @Id";
        command.Parameters.AddWithValue("@Id", id);
        
        using var reader = await command.ExecuteReaderAsync();
        
        if (await reader.ReadAsync())
        {
            return MapToUser(reader);
        }
        
        return null;
    }
    
    public async Task<User?> GetByUsernameAsync(string username)
    {
        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();
        
        var command = connection.CreateCommand();
        command.CommandText = "SELECT * FROM Users WHERE Username = @Username";
        command.Parameters.AddWithValue("@Username", username);
        
        using var reader = await command.ExecuteReaderAsync();
        
        if (await reader.ReadAsync())
        {
            return MapToUser(reader);
        }
        
        return null;
    }
    
    public async Task<User> AddAsync(User user)
    {
        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();
        
        var command = connection.CreateCommand();
        command.CommandText = @"
            INSERT INTO Users (Username, PasswordHash, Role, Status, CreatedAt, LastLoginAt)
            VALUES (@Username, @PasswordHash, @Role, @Status, @CreatedAt, @LastLoginAt);
            SELECT last_insert_rowid();";
        
        command.Parameters.AddWithValue("@Username", user.Username);
        command.Parameters.AddWithValue("@PasswordHash", user.PasswordHash);
        command.Parameters.AddWithValue("@Role", (int)user.Role);
        command.Parameters.AddWithValue("@Status", (int)user.Status);
        command.Parameters.AddWithValue("@CreatedAt", user.CreatedAt.ToString("o"));
        command.Parameters.AddWithValue("@LastLoginAt", user.LastLoginAt?.ToString("o") ?? (object)DBNull.Value);
        
        var newId = Convert.ToInt32(await command.ExecuteScalarAsync());
        user.Id = newId;
        
        return user;
    }
    
    public async Task<User> UpdateAsync(User user)
    {
        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();
        
        var command = connection.CreateCommand();
        command.CommandText = @"
            UPDATE Users 
            SET Username = @Username, 
                PasswordHash = @PasswordHash, 
                Role = @Role, 
                Status = @Status, 
                LastLoginAt = @LastLoginAt
            WHERE Id = @Id";
        
        command.Parameters.AddWithValue("@Id", user.Id);
        command.Parameters.AddWithValue("@Username", user.Username);
        command.Parameters.AddWithValue("@PasswordHash", user.PasswordHash);
        command.Parameters.AddWithValue("@Role", (int)user.Role);
        command.Parameters.AddWithValue("@Status", (int)user.Status);
        command.Parameters.AddWithValue("@LastLoginAt", user.LastLoginAt?.ToString("o") ?? (object)DBNull.Value);
        
        await command.ExecuteNonQueryAsync();
        
        return user;
    }
    
    public async Task DeleteAsync(int id)
    {
        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();
        
        var command = connection.CreateCommand();
        command.CommandText = "DELETE FROM Users WHERE Id = @Id";
        command.Parameters.AddWithValue("@Id", id);
        
        await command.ExecuteNonQueryAsync();
    }
    
    private User MapToUser(SqliteDataReader reader)
    {
        return new User
        {
            Id = reader.GetInt32(0),
            Username = reader.GetString(1),
            PasswordHash = reader.GetString(2),
            Role = (UserRole)reader.GetInt32(3),
            Status = (UserStatus)reader.GetInt32(4),
            CreatedAt = DateTime.Parse(reader.GetString(5)),
            LastLoginAt = reader.IsDBNull(6) ? null : DateTime.Parse(reader.GetString(6))
        };
    }
}
