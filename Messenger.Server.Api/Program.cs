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

var builder = WebApplication.CreateBuilder(args);

// Настройка SQLite
var connectionString = "Data Source=messenger.db";
builder.Services.AddSingleton(connectionString);

// Регистрация репозиториев
builder.Services.AddScoped<IUserRepository, UserRepository>();
builder.Services.AddScoped<IMessageRepository, MessageRepository>();
builder.Services.AddScoped<IFileAttachmentRepository, FileAttachmentRepository>();

// Регистрация сервисов
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IUserService, UserService>();
builder.Services.AddScoped<IMessageService, MessageService>();
builder.Services.AddScoped<INotificationService, NotificationService>();
builder.Services.AddScoped<IFileService, FileService>();

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

// SignalR для WebSocket уведомлений
builder.Services.AddSignalR();

var app = builder.Build();

// Инициализация БД
using (var scope = app.Services.CreateScope())
{
    var connectionString = scope.ServiceProvider.GetRequiredService<string>();
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
            Type INTEGER NOT NULL,
            Content TEXT NOT NULL,
            CreatedAt TEXT NOT NULL,
            IsDeleted INTEGER NOT NULL DEFAULT 0,
            FOREIGN KEY (SenderId) REFERENCES Users(Id)
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
        
        Console.WriteLine("=== DEFAULT ADMIN CREATED ===");
        Console.WriteLine("Username: admin");
        Console.WriteLine("Password: admin123");
        Console.WriteLine("=============================");
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

// Порт 748 для клиентов
app.Urls.Add("http://*:748");

Console.WriteLine("Server API started on port 748");

// Создание папки для загрузок
var uploadFolder = Path.Combine(Directory.GetCurrentDirectory(), "uploads");
if (!Directory.Exists(uploadFolder))
{
    Directory.CreateDirectory(uploadFolder);
    Console.WriteLine($"Upload folder created: {uploadFolder}");
}

// Middleware для проверки localhost на админских эндпоинтах
app.UseWhen(
    context => context.Request.Path.StartsWithSegments("/api/users"),
    appBuilder =>
    {
        appBuilder.UseMiddleware<AdminAuthMiddleware>();
    });

app.Run();
