using Messenger.Admin.Web.Components;
using Messenger.Admin.Web.Services;

var builder = WebApplication.CreateBuilder(args);

// Add Blazor Server services
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

// Register HttpClient for API calls
builder.Services.AddHttpClient<ApiService>();

// Register ApiService as scoped
builder.Services.AddScoped<ApiService>();

var app = builder.Build();

// Configure the HTTP request pipeline
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
}

app.UseStaticFiles();
app.UseAntiforgery();

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

// Порт 228 для админки
app.Urls.Add("http://*:228");

Console.WriteLine("Admin Web started on port 228");
app.Run();
