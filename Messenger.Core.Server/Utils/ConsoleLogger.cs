namespace Messenger.Core.Utils;

/// <summary>
/// Уровни логирования
/// </summary>
public enum LogLevel
{
    Info,
    Warning,
    Error
}

/// <summary>
/// Утилита для консольного логирования с префиксами уровней
/// </summary>
public static class ConsoleLogger
{
    private static readonly object _lock = new();

    /// <summary>
    /// Логирование информационного сообщения
    /// </summary>
    public static void Info(string message)
    {
        Log(LogLevel.Info, message);
    }

    /// <summary>
    /// Логирование предупреждения
    /// </summary>
    public static void Warn(string message)
    {
        Log(LogLevel.Warning, message);
    }

    /// <summary>
    /// Логирование ошибки
    /// </summary>
    public static void Error(string message)
    {
        Log(LogLevel.Error, message);
    }

    /// <summary>
    /// Логирование ошибки с исключением
    /// </summary>
    public static void Error(string message, Exception ex)
    {
        Log(LogLevel.Error, $"{message} Exception: {ex.Message}");
    }

    private static void Log(LogLevel level, string message)
    {
        lock (_lock)
        {
            var prefix = level switch
            {
                LogLevel.Info => "[INFO]",
                LogLevel.Warning => "[WARN]",
                LogLevel.Error => "[ERROR]"
            };

            var timestamp = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss.fff");
            Console.WriteLine($"{timestamp} {prefix} {message}");
        }
    }
}
