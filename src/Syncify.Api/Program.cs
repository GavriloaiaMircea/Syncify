using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

var webOrigins = builder.Configuration.GetSection("Cors:WebOrigins").Get<string[]>() ?? [];

builder.Services.AddCors(options =>
{
    options.AddPolicy("Web", policy =>
        policy.WithOrigins(webOrigins)
            .AllowAnyHeader()
            .AllowAnyMethod());
});
builder.Services.AddOpenApi();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference();
}

app.UseCors("Web");

app.MapGet("/health", () => Results.Ok(new
{
    status = "ok",
    service = "syncify-api",
    timestamp = DateTimeOffset.UtcNow
}));

app.Run();
