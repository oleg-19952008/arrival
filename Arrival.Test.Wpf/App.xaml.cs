using System.Windows;

namespace Arrival.Test.Wpf
{
    public partial class App : Application
    {
        // Статические поля для хранения данных аутентификации
        public static string? CurrentToken { get; set; }
        public static int? CurrentUserId { get; set; }
        public static string? CurrentUsername { get; set; }
        public static string? CurrentRole { get; set; }
        public static string? CurrentStatus { get; set; }
    }
}
