var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

app.MapGet("/health", () => Results.Json(new { status = "ok" }));
app.MapGet("/api/items", () => Results.Json(new[]
{
    new { id = 1, name = "alpha" },
    new { id = 2, name = "beta" },
}));
app.MapGet("/openapi.yaml", () => Results.Text("""
openapi: 3.0.3
info:
  title: Sample API
  version: 1.0.0
paths:
  /health:
    get:
      responses:
        '200':
          description: OK
""", "application/yaml"));

app.Run();

public partial class Program;
