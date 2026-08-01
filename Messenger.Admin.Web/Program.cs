using Microsoft.AspNetCore.StaticFiles;

var builder = WebApplication.CreateBuilder(args);

// Configure Kestrel to listen on port 228
builder.WebHost.ConfigureKestrel(options =>
{
    options.ListenAnyIP(228);
});

var app = builder.Build();

// Configure the HTTP request pipeline
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
}

// Enable default content type provider for static files
var provider = new FileExtensionContentTypeProvider();
provider.Mappings[".webp"] = "image/webp";

app.UseStaticFiles(new StaticFileOptions
{
    ContentTypeProvider = provider
});

// Serve index.html as default file
app.UseDefaultFiles(new DefaultFilesOptions
{
    DefaultFileNames = new List<string> { "index.html" }
});

// Redirect root to index.html
app.MapGet("/", () => Results.Redirect("/index.html"));

Console.WriteLine("Admin Web started on port 228");
app.Run();
