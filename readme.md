markdown

# Техническое Задание: Messenger.Core.Server

## 1. Общее описание

**Messenger.Core.Server** — ядро сервера для текстового мессенджера, предназначенное для работы в локальной сети с 3-4 пользователями. Консольное приложение Windows, которое поднимает веб-сервер и управляет пользователями, сообщениями и файлами.

### Архитектура

- Консольное приложение (.NET 8.0, Windows)
- Поднимает ASP.NET Core Kestrel на двух портах одновременно
- REST API для клиентов (порт 123)
- REST API для администратора (порт 228, только localhost)
- WebSocket для real-time сообщений
- SQLite БД для хранения данных

### Запуск

Messenger.Core.Server.exe
→ [INFO] Server started on port 123
→ [INFO] Admin panel on port 228
→ (консоль остаётся открытой, выводит логи)
→ Остановка: Ctrl+C


---

## 2. Требования

### 2.1 Управление пользователями

#### Регистрация

- Принимает: `username`, `password`
- Проверяет: username не занят
- Создаёт пользователя со статусом `PendingApproval`
- Хеширует пароль (BCrypt)
- Возвращает: `userId`, `username`, `status`
- Логирует в консоль: `[INFO] User registered: {username}`

#### Аутентификация

- Принимает: `username`, `password`
- Проверяет: пользователь существует, пароль верный, статус = `Active`
- Возвращает: JWT token (expire = 7 дней) или ошибку
- При статусе ≠ Active: отказывает в доступе с ошибкой
- Логирует в консоль: `[INFO] User logged in: {username}` или `[WARN] Failed login: {username}`

#### Управление (только админ через localhost:228)

- Одобрение пользователя (PendingApproval → Active)
- Банирование пользователя (Active → Banned)
- Разбанирование пользователя (Banned → Active)
- Удаление пользователя (любой статус → Deleted)
- Смена пароля пользователя
- Логирует в консоль: `[ADMIN] User {userId} approved` и т.д.

#### Получение данных

- Список всех пользователей с фильтром по статусу
- Информация о конкретном пользователе

### 2.2 Сообщения

#### Отправка

- Принимает: `userId` (из JWT token), `text`
- Сохраняет: `senderId`, `text`, `timestamp`
- Возвращает: `messageId`
- Требует: `user.status = Active`
- Логирует в консоль: `[INFO] Message from {username}: {text}` (первые 50 символов)

#### Получение

- История последних N сообщений (limit, offset)
- Фильтр по userId отправителя (опционально)
- Сортировка по timestamp (новые в конце)
- Исключает удалённые сообщения (IsDeleted = true)

#### Удаление

- Мягкое удаление (флаг IsDeleted в БД, физически не удаляется)
- При удалении → удаляются прикреплённые файлы с диска
- Логирует в консоль: `[INFO] Message {messageId} deleted`

#### Шифрование

- Текст сообщения может быть зашифрован на стороне клиента
- Сервер хранит как есть (не требует расшифровки)

### 2.3 Файлы

#### Загрузка

- Принимает: multipart файл
- Сохраняет на диск в папку `uploads/` (создаётся при первом запуске)
- Генерирует fileId (UUID v4)
- Возвращает: `fileId`, `size`, `fileName`
- Логирует в консоль: `[INFO] File uploaded: {fileName} ({size} bytes)`

#### Скачивание

- По fileId отправляет файл клиенту
- Проверяет: файл существует
- Возвращает 404 если файл не найден

#### Удаление

- При мягком удалении сообщения → удаляются прикреплённые файлы с диска
- Логирует в консоль: `[INFO] File deleted: {fileId}`

### 2.4 WebSocket (Real-time)

#### Подключение

- Клиент подключается к `ws://server:123/ws`
- Передаёт JWT token в query параметре или заголовке
- Сервер валидирует token, иначе отказывает в подключении
- Сервер хранит соединение в памяти (WebSocketManager)
- Логирует в консоль: `[INFO] Client connected: {username}`

#### События (сервер → клиент)

```json
{
  "type": "message",
  "id": 5,
  "senderId": 1,
  "senderUsername": "user1",
  "text": "Привет!",
  "timestamp": "2024-01-15T10:31:00Z"
}
```

```json
{
  "type": "user_joined",
  "userId": 2,
  "username": "user2"
}
```

```json
{
  "type": "user_left",
  "userId": 2,
  "username": "user2"
}
```

#### Broadcast

- При новом сообщении → отправляет всем подключённым клиентам (кроме отправителя)
- При подключении → уведомляет остальных что пользователь вошёл
- При отключении → уведомляет остальных что пользователь вышел

---

## 3. База данных (SQLite)

**Файл:** `messenger.db` (создаётся в папке с приложением при первом запуске)

### Таблица: Users

```sql
CREATE TABLE Users (
  Id INTEGER PRIMARY KEY AUTOINCREMENT,
  Username TEXT UNIQUE NOT NULL,
  PasswordHash TEXT NOT NULL,
  Status TEXT NOT NULL,
  CreatedAt DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP
);

CREATE UNIQUE INDEX idx_users_username ON Users(Username);
```

**Статусы:** `PendingApproval` | `Active` | `Banned` | `Deleted`

### Таблица: Messages

```sql
CREATE TABLE Messages (
  Id INTEGER PRIMARY KEY AUTOINCREMENT,
  SenderId INTEGER NOT NULL,
  Text TEXT NOT NULL,
  IsDeleted BOOLEAN DEFAULT 0,
  Timestamp DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
  FOREIGN KEY (SenderId) REFERENCES Users(Id)
);

CREATE INDEX idx_messages_senderid ON Messages(SenderId);
CREATE INDEX idx_messages_timestamp ON Messages(Timestamp);
```

### Таблица: FileAttachments

```sql
CREATE TABLE FileAttachments (
  Id INTEGER PRIMARY KEY AUTOINCREMENT,
  MessageId INTEGER,
  FileId TEXT UNIQUE NOT NULL,
  FileName TEXT NOT NULL,
  Size INTEGER,
  UploadedAt DATETIME DEFAULT CURRENT_TIMESTAMP,
  FilePath TEXT NOT NULL,
  FOREIGN KEY (MessageId) REFERENCES Messages(Id)
);

CREATE UNIQUE INDEX idx_fileattachments_fileid ON FileAttachments(FileId);
CREATE INDEX idx_fileattachments_messageid ON FileAttachments(MessageId);
```

---

## 4. REST API

### 4.1 Публичный API (порт 123)

#### POST /api/auth/register

**Request:**
```json
{
  "username": "user1",
  "password": "pass123"
}
```

**Response (201 Created):**
```json
{
  "userId": 1,
  "username": "user1",
  "status": "PendingApproval"
}
```

**Response (400 Bad Request):**
```json
{
  "error": "Username already taken"
}
```

---

#### POST /api/auth/login

**Request:**
```json
{
  "username": "user1",
  "password": "pass123"
}
```

**Response (200 OK):**
```json
{
  "token": "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...",
  "userId": 1,
  "username": "user1",
  "expiresIn": 604800
}
```

**Response (401 Unauthorized):**
```json
{
  "error": "Invalid credentials or account not approved"
}
```

---

#### GET /api/messages

**Query params:** `?limit=50&offset=0`

**Headers:** `Authorization: Bearer <token>`

**Response (200 OK):**
```json
[
  {
    "id": 1,
    "senderId": 1,
    "senderUsername": "user1",
    "text": "Привет!",
    "timestamp": "2024-01-15T10:30:00Z"
  },
  {
    "id": 2,
    "senderId": 2,
    "senderUsername": "user2",
    "text": "Привет всем!",
    "timestamp": "2024-01-15T10:31:00Z"
  }
]
```

**Response (401 Unauthorized):**
```json
{
  "error": "Invalid token"
}
```

---

#### POST /api/messages

**Headers:** `Authorization: Bearer <token>`

**Request:**
```json
{
  "text": "Привет всем!"
}
```

**Response (201 Created):**
```json
{
  "messageId": 5,
  "timestamp": "2024-01-15T10:31:00Z"
}
```

**Response (401 Unauthorized):**
```json
{
  "error": "Invalid token"
}
```

**Response (403 Forbidden):**
```json
{
  "error": "User account is not approved"
}
```

---

#### DELETE /api/messages/{messageId}

**Headers:** `Authorization: Bearer <token>`

**Response (200 OK):**
```json
{
  "deleted": true
}
```

**Response (403 Forbidden):**
```json
{
  "error": "Not your message"
}
```

**Response (404 Not Found):**
```json
{
  "error": "Message not found"
}
```

---

#### POST /api/upload

**Headers:** `Authorization: Bearer <token>`

**Request:**

Content-Type: multipart/form-data

    file: <бинарные данные>


**Response (200 OK):**
```json
{
  "fileId": "550e8400-e29b-41d4-a716-446655440000",
  "fileName": "photo.jpg",
  "size": 204800
}
```

**Response (401 Unauthorized):**
```json
{
  "error": "Invalid token"
}
```

**Response (413 Payload Too Large):**
```json
{
  "error": "File too large"
}
```

---

#### GET /api/download/{fileId}

**Headers:** `Authorization: Bearer <token>`

**Response (200 OK):**

<бинарные данные файла>
Content-Disposition: attachment; filename="photo.jpg"


**Response (404 Not Found):**
```json
{
  "error": "File not found"
}
```

---

### 4.2 Админ API (порт 228, только localhost)

#### GET /admin/api/users

**Response (200 OK):**
```json
[
  {
    "id": 1,
    "username": "user1",
    "status": "Active",
    "createdAt": "2024-01-10T15:00:00Z"
  },
  {
    "id": 2,
    "username": "user2",
    "status": "PendingApproval",
    "createdAt": "2024-01-12T12:00:00Z"
  },
  {
    "id": 3,
    "username": "user3",
    "status": "Banned",
    "createdAt": "2024-01-09T10:00:00Z"
  }
]
```

**Response (403 Forbidden):**
```json
{
  "error": "Access denied (not localhost)"
}
```

---

#### POST /admin/api/users/{userId}/approve

**Response (200 OK):**
```json
{
  "userId": 2,
  "status": "Active"
}
```

**Response (404 Not Found):**
```json
{
  "error": "User not found"
}
```

---

#### POST /admin/api/users/{userId}/ban

**Response (200 OK):**
```json
{
  "userId": 1,
  "status": "Banned"
}
```

---

#### POST /admin/api/users/{userId}/unban

**Response (200 OK):**
```json
{
  "userId": 1,
  "status": "Active"
}
```

---

#### DELETE /admin/api/users/{userId}

**Response (200 OK):**
```json
{
  "deleted": true,
  "userId": 2
}
```

**Response (404 Not Found):**
```json
{
  "error": "User not found"
}
```

---

#### POST /admin/api/users/{userId}/password

**Request:**
```json
{
  "newPassword": "newpass123"
}
```

**Response (200 OK):**
```json
{
  "userId": 1,
  "updated": true
}
```

**Response (404 Not Found):**
```json
{
  "error": "User not found"
}
```

---

## 5. WebSocket API

### Подключение

ws://server:123/ws?token=<JWT_TOKEN>


Или в заголовке:

Authorization: Bearer <JWT_TOKEN>


### Сообщения от сервера (JSON)

**Новое сообщение:**
```json
{
  "type": "message",
  "id": 5,
  "senderId": 1,
  "senderUsername": "user1",
  "text": "Привет!",
  "timestamp": "2024-01-15T10:31:00Z"
}
```

**Пользователь вошёл:**
```json
{
  "type": "user_joined",
  "userId": 2,
  "username": "user2"
}
```

**Пользователь вышел:**
```json
{
  "type": "user_left",
  "userId": 2,
  "username": "user2"
}
```

---

## 6. Сервисы

### AuthService

```csharp
Task<User> RegisterAsync(string username, string password)
Task<(User user, string token)> LoginAsync(string username, string password)
Task ApproveUserAsync(int userId)
Task BanUserAsync(int userId)
Task UnbanUserAsync(int userId)
Task DeleteUserAsync(int userId)
Task ChangePasswordAsync(int userId, string newPassword)
Task<IEnumerable<User>> GetAllUsersAsync()
Task<User> GetUserByIdAsync(int userId)
Task<User> GetUserByUsernameAsync(string username)
string GenerateJwtToken(User user)
```

### MessageService

```csharp
Task<ChatMessage> SendMessageAsync(int senderId, string text)
Task<IEnumerable<ChatMessage>> GetMessagesAsync(int limit, int offset)
Task<IEnumerable<ChatMessage>> GetMessagesByUserAsync(int senderId, int limit)
Task DeleteMessageAsync(int messageId, int requestedByUserId)
Task<ChatMessage> GetMessageByIdAsync(int messageId)
```

### FileService

```csharp
Task<FileAttachment> SaveFileAsync(Stream fileStream, string fileName)
Task<byte[]> GetFileAsync(string fileId)
Task<FileAttachment> GetFileAttachmentAsync(string fileId)
Task DeleteFileAsync(string fileId)
string GetFilePathAsync(string fileId)
```

### WebSocketManager

```csharp
Task HandleClientAsync(WebSocket socket, int userId, string username)
Task BroadcastMessageAsync(ChatMessage message)
Task NotifyUserJoinedAsync(int userId, string username)
Task NotifyUserLeftAsync(int userId, string username)
IReadOnlyCollection<int> GetConnectedUsers()
bool IsUserConnected(int userId)
Task DisconnectAsync(int userId)
```

---

## 7. Консольное приложение

### Program.cs (точка входа)

**Структура:**
- Создание DbContext с SQLite
- Регистрация всех сервисов (DI)
- Конфигурация Kestrel для портов 123 и 228
- Инициализация БД (EnsureCreated или миграции)
- Регистрация контроллеров
- Регистрация WebSocket обработчика
- Запуск приложения
- Блокирование главного потока (WaitHandle или Console.ReadLine)

### Консольный вывод (примеры)

[INFO] Initializing database...
[INFO] Database initialized successfully
[INFO] Registering services...
[INFO] Server started on port 123
[INFO] Admin panel on port 228
[INFO] Waiting for connections...
[INFO] User registered: user1
[WARN] Failed login attempt: user2 (invalid password)
[INFO] User logged in: user1
[INFO] Message from user1: Привет!
[INFO] File uploaded: photo.jpg (204800 bytes)
[ADMIN] User 2 approved
[ADMIN] User 1 banned
[INFO] Client connected: user1
[INFO] Client disconnected: user2
[ERROR] Database error: Connection failed


---

## 8. Безопасность

- **JWT Token**: подписан секретным ключом (256-бит), expire = 7 дней, алгоритм HS256
- **Пароли**: BCrypt хеширование (не хранить в открытом виде)
- **Админка**: проверка localhost IP адреса в middleware (отказ если не 127.0.0.1)
- **WebSocket**: требует валидного JWT token при подключении
- **HTTPS/WSS**: если используется в боевом окружении, обязательно
- **Файлы**: валидация типа файла (опционально), ограничение размера (опционально)

---

## 9. Технический стек

| Компонент | Версия |
|-----------|--------|
| .NET | 8.0 |
| ОС | Windows |
| Тип приложения | Консольное (Console Application) |
| Framework | ASP.NET Core (Kestrel) |
| БД | SQLite |
| ORM | Entity Framework Core |
| Криптография | System.Security.Cryptography, BCrypt.Net-Next |
| Сериализация | System.Text.Json |
| WebSocket | встроенный в ASP.NET Core |
| JWT | System.IdentityModel.Tokens.Jwt |

---

## 10. Структура проекта

Messenger.Core.Server/
├── Program.cs ← точка входа, конфигурация Kestrel
├── Messenger.Core.Server.csproj ← файл проекта
│
├── Data/
│ ├── MessengerDbContext.cs ← DbContext
│ └── Migrations/ ← папка для миграций EF Core
│
├── Models/
│ ├── User.cs
│ ├── ChatMessage.cs
│ └── FileAttachment.cs
│
├── Services/
│ ├── AuthService.cs
│ ├── MessageService.cs
│ ├── FileService.cs
│ ├── WebSocketManager.cs
│ ├── IAuthService.cs
│ ├── IMessageService.cs
│ ├── IFileService.cs
│ └── IWebSocketManager.cs
│
├── Controllers/
│ ├── AuthController.cs ← POST /auth/register, /auth/login
│ ├── MessagesController.cs ← GET/POST/DELETE /messages
│ ├── FileController.cs ← POST /upload, GET /download
│ └── AdminController.cs ← /admin/api/users и т.д.
│
├── Middleware/
│ └── AdminAuthMiddleware.cs ← проверка localhost для /admin/*
│
├── Utils/
│ ├── JwtTokenGenerator.cs
│ ├── PasswordHasher.cs
│ └── Logger.cs ← логирование в консоль
│
├── uploads/ ← папка для загруженных файлов (создаётся автоматически)
└── messenger.db ← SQLite БД (создаётся автоматически)


---

## 11. Критерии приёма

- ✅ Консольное приложение запускается, поднимает два сервера на портах 123 и 228
- ✅ Логирование всех операций в консоль в реальном времени
- ✅ Регистрация создаёт пользователя в статусе PendingApproval
- ✅ Логин работает только если статус Active, возвращает JWT token
- ✅ Сообщения сохраняются в БД и broadcastятся по WebSocket всем подключённым
- ✅ Файлы загружаются на диск в папку uploads/, скачиваются по fileId
- ✅ Админ-панель (порт 228) доступна только с localhost (127.0.0.1)
- ✅ Админ может одобрять, банить, разбанивать, удалять пользователей и менять пароли
- ✅ БД создаётся автоматически при первом запуске (messenger.db)
- ✅ Graceful shutdown на Ctrl+C (закрытие соединений, сохранение данных)
- ✅ Все сообщения об ошибках выводятся в консоль с пометкой [ERROR]

---

## 12. Дополнительные требования

- Сервис должен быть максимально простым и без лишних зависимостей
- Код должен быть читаемым и хорошо структурированным
- Использовать async/await для всех I/O операций
- Использовать dependency injection для всех сервисов
- Все API ошибки должны возвращать корректные HTTP коды (400, 401, 403, 404, 500)
 