using System.Text.Json.Serialization;
using Scalar.AspNetCore;
using Syncify.Api.Endpoints;
using Syncify.Application;
using Syncify.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

var webOrigins = builder.Configuration.GetSection("Cors:WebOrigins").Get<string[]>() ?? [];

builder.Services.AddCors(options =>
{
    options.AddPolicy("Web", policy =>
        policy.WithOrigins(webOrigins)
            .AllowAnyHeader()
            .AllowAnyMethod());
});
builder.Services.ConfigureHttpJsonOptions(options =>
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));
builder.Services.AddOpenApi();
builder.Services.AddApplication();
builder.Services.AddInfrastructure();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference();
    app.MapDevEndpoints();
}

app.UseCors("Web");

app.MapGet("/health", () => Results.Ok(new
{
    status = "ok",
    service = "syncify-api",
    timestamp = DateTimeOffset.UtcNow
}));

app.Run();
