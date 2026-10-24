var builder = WebApplication.CreateBuilder(args);
builder.WebHost.UseUrls("http://localhost:5080");

var app = builder.Build();

app.MapGet("/api/hello", () => Results.Text("Hello from the sample API.", "text/plain"));

app.MapPost("/api/echo", async (HttpRequest request) =>
{
    using var reader = new StreamReader(request.Body);
    var body = await reader.ReadToEndAsync();
    return Results.Text(body, request.ContentType);
});

app.Run();
