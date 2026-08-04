var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

app.MapGet("/", () => Results.Content("""
    <!DOCTYPE html>
    <html lang="en">
    <head>
      <meta charset="utf-8"/>
      <title>ARTR Pien Sample Site</title>
      <meta name="description" content="Local sample target for Pien functional tests."/>
    </head>
    <body>
      <h1>Sample Site</h1>
      <p>Authorized local target for ARTR Pien verification.</p>
      <a href="/about">About</a>
    </body>
    </html>
    """, "text/html"));

app.MapGet("/about", () => Results.Content("""
    <!DOCTYPE html>
    <html lang="en"><head><title>About</title></head><body><h1>About</h1></body></html>
    """, "text/html"));

app.MapGet("/headers-ok", (HttpResponse response) =>
{
    response.Headers["Content-Security-Policy"] = "default-src 'self'";
    response.Headers["X-Content-Type-Options"] = "nosniff";
    response.Headers["Referrer-Policy"] = "no-referrer";
    response.Headers["Permissions-Policy"] = "geolocation=()";
    return Results.Content("<html lang=\"en\"><head><title>Headers</title></head><body>ok</body></html>", "text/html");
});

app.Run();

public partial class Program;
