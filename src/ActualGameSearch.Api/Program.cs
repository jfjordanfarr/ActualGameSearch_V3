using ActualGameSearch.ServiceDefaults;

var builder = WebApplication.CreateBuilder(args);
builder.AddServiceDefaults();

var app = builder.Build();

app.UseDefaultFiles();
app.UseStaticFiles();

app.MapGet("/health", () => Results.Ok(new { status = "ok" }));

// Temporary placeholders to support frontend wiring; real implementations will follow tasks
app.MapGet("/api/search/games", (string q) => Results.Json(new { items = Array.Empty<object>() }));
app.MapGet("/api/search/reviews", (string q) => Results.Json(new { items = Array.Empty<object>() }));

app.Run();
