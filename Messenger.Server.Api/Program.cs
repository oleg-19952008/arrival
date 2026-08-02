using Messenger.Core.Interfaces;
using Messenger.Core.Services;
using Messenger.Core.Data;
using Microsoft.Data.Sqlite;
using System.IdentityModel.Tokens.Jwt;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using Messenger.Server.Api.Hubs;
using Messenger.Server.Api.Middleware;
using Messenger.Core.Utils;
using Microsoft.AspNetCore.StaticFiles;

var builder = WebApplication.CreateBuilder(args);

// Настройка Kestrel для прослушивания обоих портов
builder.WebHost.ConfigureKestrel(options =>
{
    options.ListenAnyIP(748); // API порт
    options.ListenAnyIP(228); // Админ порт
});

// Настройка SQLite с абсолютным путем
var dbPath = Path.Combine(AppContext.BaseDirectory, "messenger.db");
ConsoleLogger.Info($"Database path: {dbPath}");

// Формируем строку подключения напрямую
var connectionString = $"Data Source={dbPath}";
ConsoleLogger.Info($"Connection string: {connectionString}");
ConsoleLogger.Info($"Connection string bytes: {string.Join(",", System.Text.Encoding.UTF8.GetBytes(connectionString))}");

// Регистрация репозиториев с передачей строки подключения
builder.Services.AddScoped<IUserRepository>(sp => new UserRepository(connectionString));
builder.Services.AddScoped<IMessageRepository>(sp => new MessageRepository(connectionString));
builder.Services.AddScoped<IFileAttachmentRepository>(sp => new FileAttachmentRepository(connectionString));

// Регистрация сервисов
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IUserService, UserService>();
builder.Services.AddScoped<IMessageService, MessageService>();
builder.Services.AddScoped<INotificationService, NotificationService>();
builder.Services.AddScoped<IFileService, FileService>();

// SignalR для WebSocket уведомлений
builder.Services.AddSignalR();

// JWT настройка
var jwtKey = "YourSuperSecretKeyForMessengerCoreServer2024WithMinimum32BytesLength!";
var jwtIssuer = "Messenger.Server.Api";

builder.Services.AddSingleton(jwtKey);

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = false,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        ValidIssuer = jwtIssuer,
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey))
    };
});

builder.Services.AddAuthorization();

// CORS для локальной сети
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowLocalNetwork", policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyHeader()
              .AllowAnyMethod();
    });
});

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.AddSecurityDefinition("Bearer", new Microsoft.OpenApi.Models.OpenApiSecurityScheme
    {
        Description = "JWT Authorization header using the Bearer scheme. Enter 'Bearer' [space] and then your token.",
        Name = "Authorization",
        In = Microsoft.OpenApi.Models.ParameterLocation.Header,
        Type = Microsoft.OpenApi.Models.SecuritySchemeType.ApiKey,
        Scheme = "Bearer"
    });
    
    c.AddSecurityRequirement(new Microsoft.OpenApi.Models.OpenApiSecurityRequirement
    {
        {
            new Microsoft.OpenApi.Models.OpenApiSecurityScheme
            {
                Reference = new Microsoft.OpenApi.Models.OpenApiReference
                {
                    Type = Microsoft.OpenApi.Models.ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            Array.Empty<string>()
        }
    });
});

var app = builder.Build();

// Инициализация БД
using (var scope = app.Services.CreateScope())
{
    using var connection = new SqliteConnection(connectionString);
    connection.Open();
    
    var createUsersTable = @"
        CREATE TABLE IF NOT EXISTS Users (
            Id INTEGER PRIMARY KEY AUTOINCREMENT,
            Username TEXT NOT NULL UNIQUE,
            PasswordHash TEXT NOT NULL,
            Role INTEGER NOT NULL,
            Status INTEGER NOT NULL,
            CreatedAt TEXT NOT NULL,
            LastLoginAt TEXT
        )";
    
    var createMessagesTable = @"
        CREATE TABLE IF NOT EXISTS Messages (
            Id INTEGER PRIMARY KEY AUTOINCREMENT,
            SenderId INTEGER NOT NULL,
            SenderName TEXT,
            RecipientId INTEGER,
            Type INTEGER NOT NULL,
            Content TEXT NOT NULL,
            CreatedAt TEXT NOT NULL,
            IsDeleted INTEGER NOT NULL DEFAULT 0,
            FOREIGN KEY (SenderId) REFERENCES Users(Id),
            FOREIGN KEY (RecipientId) REFERENCES Users(Id)
        )";
    
    using var cmd1 = new Microsoft.Data.Sqlite.SqliteCommand(createUsersTable, connection);
    cmd1.ExecuteNonQuery();
    
    using var cmd2 = new Microsoft.Data.Sqlite.SqliteCommand(createMessagesTable, connection);
    cmd2.ExecuteNonQuery();
    
    // Создание администратора по умолчанию, если он не существует
    var checkAdmin = "SELECT COUNT(*) FROM Users WHERE Username = 'admin'";
    using var cmdCheck = new Microsoft.Data.Sqlite.SqliteCommand(checkAdmin, connection);
    var adminCount = Convert.ToInt32(cmdCheck.ExecuteScalar());
    
    if (adminCount == 0)
    {
        var passwordHasher = new Messenger.Core.Utils.PasswordHasher();
        var adminPassword = passwordHasher.HashPassword("admin123");
        var insertAdmin = @"
            INSERT INTO Users (Username, PasswordHash, Role, Status, CreatedAt)
            VALUES ('admin', @PasswordHash, 1, 1, @CreatedAt)";
        
        using var cmdInsert = new Microsoft.Data.Sqlite.SqliteCommand(insertAdmin, connection);
        cmdInsert.Parameters.AddWithValue("@PasswordHash", adminPassword);
        cmdInsert.Parameters.AddWithValue("@CreatedAt", DateTime.UtcNow.ToString("O"));
        cmdInsert.ExecuteNonQuery();
        
        ConsoleLogger.Info("=== DEFAULT ADMIN CREATED ===");
        ConsoleLogger.Info("Username: admin");
        ConsoleLogger.Info("Password: admin123");
        ConsoleLogger.Info("=============================");
    }
}

app.UseCors("AllowLocalNetwork");
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapHub<NotificationHub>("/notificationHub");

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// Создание папки для загрузок
var uploadFolder = Path.Combine(Directory.GetCurrentDirectory(), "uploads");
if (!Directory.Exists(uploadFolder))
{
    Directory.CreateDirectory(uploadFolder);
    ConsoleLogger.Info($"Upload folder created: {uploadFolder}");
}

// Middleware для проверки localhost на админских эндпоинтах
app.UseWhen(
    context => context.Request.Path.StartsWithSegments("/api/users"),
    appBuilder =>
    {
        appBuilder.UseMiddleware<AdminAuthMiddleware>();
    });

// Настройка раздачи статических файлов для админ-панели на порту 228
var provider = new FileExtensionContentTypeProvider();
provider.Mappings[".webp"] = "image/webp";

// Обработка запросов на порт 228 (админ-панель)
app.UseWhen(
    context => context.Connection.LocalPort == 228,
    appBuilder =>
    {
        appBuilder.UseDefaultFiles(new DefaultFilesOptions
        {
            DefaultFileNames = new List<string> { "index.html" }
        });
        appBuilder.UseStaticFiles(new StaticFileOptions
        {
            ContentTypeProvider = provider
        });
    });

// Консольный интерфейс для управления сервером
ConsoleLogger.Info("===========================================");
ConsoleLogger.Info("Server started on ports 748 (API) and 228 (Admin).");
ConsoleLogger.Info("Type 'exit' to stop.");
ConsoleLogger.Info("===========================================");

// Запуск в отдельном потоке для возможности обработки команд консоли
var runTask = Task.Run(() => app.Run());

// Обработка команд консоли
while (true)
{
    var input = Console.ReadLine();
    if (input?.Trim().ToLower() == "exit")
    {
        ConsoleLogger.Info("Shutting down server...");
        break;
    }
}

// Остановка приложения
await app.StopAsync();
