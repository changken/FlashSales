using System.Text.Json;
using System.Text.Json.Serialization;
using Dapper;
using FlashSales.Api.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

// JSON snake_case serialization (matching Go API contract)
builder.Services.AddControllers().AddJsonOptions(options =>
{
    options.JsonSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower;
    options.JsonSerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
});

// Swagger
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new() { Title = "Flash Sale API", Version = "v1" });
});

// Database connection string
var connectionString = Environment.GetEnvironmentVariable("DATABASE_URL")
    ?? builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("DATABASE_URL or ConnectionStrings:DefaultConnection is required");

// Dapper: map snake_case DB columns to PascalCase properties
DefaultTypeMap.MatchNamesWithUnderscores = true;

// Register all services
builder.Services.AddFlashSalesServices(connectionString);

var app = builder.Build();

// Swagger UI
app.UseSwagger();
app.UseSwaggerUI(c => c.SwaggerEndpoint("/swagger/v1/swagger.json", "Flash Sale API v1"));

app.MapControllers();

var port = Environment.GetEnvironmentVariable("PORT") ?? "8080";
app.Urls.Add($"http://0.0.0.0:{port}");

app.Logger.LogInformation("Server starting on :{Port}  swagger: http://localhost:{Port}/swagger/index.html", port, port);
app.Run();
