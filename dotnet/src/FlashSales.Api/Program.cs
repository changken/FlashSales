using System.Text.Json;
using System.Text.Json.Serialization;
using Dapper;
using FlashSales.Api.Filters;
using FlashSales.Api.Infrastructure;
using Prometheus;
using StackExchange.Redis;

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

// Redis connection (lazy factory so WebApplicationFactory can override before Connect() is called)
var redisConnection = Environment.GetEnvironmentVariable("REDIS_URL") ?? "localhost:6379";
builder.Services.AddSingleton<IConnectionMultiplexer>(_ => ConnectionMultiplexer.Connect(redisConnection));

// Dapper: map snake_case DB columns to PascalCase properties
DefaultTypeMap.MatchNamesWithUnderscores = true;

// Register all services
builder.Services.AddFlashSalesServices(connectionString);
builder.Services.AddScoped<RateLimitFilter>();

var app = builder.Build();

app.UseExceptionHandler("/error");
app.Map("/error", (HttpContext ctx) =>
    Results.Problem(statusCode: 500, title: "An unexpected error occurred."));

// Prometheus metrics endpoint
app.UseMetricServer();
app.UseHttpMetrics();

// Swagger UI
app.UseSwagger();
app.UseSwaggerUI(c => c.SwaggerEndpoint("/swagger/v1/swagger.json", "Flash Sale API v1"));

app.MapControllers();

var port = Environment.GetEnvironmentVariable("PORT") ?? "8080";
app.Urls.Add($"http://0.0.0.0:{port}");

app.Logger.LogInformation("Server starting on :{Port}  swagger: http://localhost:{Port}/swagger/index.html", port, port);
app.Logger.LogInformation("Metrics available at http://localhost:{Port}/metrics", port);
app.Run();

// Required for WebApplicationFactory in integration tests
public partial class Program { }
