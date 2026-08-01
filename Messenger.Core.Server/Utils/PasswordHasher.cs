namespace Messenger.Core.Utils;

/// <summary>
/// Утилита для хеширования паролей
/// </summary>
public class PasswordHasher
{
    /// <summary>
    /// Хеширует пароль с использованием BCrypt
    /// </summary>
    public string HashPassword(string password)
    {
        return BCrypt.Net.BCrypt.HashPassword(password);
    }

    /// <summary>
    /// Проверяет соответствие пароля хешу
    /// </summary>
    public bool VerifyPassword(string password, string hash)
    {
        return BCrypt.Net.BCrypt.Verify(password, hash);
    }
}
