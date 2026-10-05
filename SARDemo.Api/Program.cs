using Microsoft.Extensions.Http.Resilience;
using Polly;
using Polly.CircuitBreaker;
using Polly.RateLimiting;
using Polly.Retry;
using Polly.Timeout;
using SARDemo.Api.HealthChecks;
using SARDemo.Api.Services;
using SARDemo.Api.Services.Implementations;
using System.Threading.RateLimiting;

var builder = WebApplication.CreateBuilder(args);

//  SCALABILITY: Rate Limiter
builder.Services.AddRateLimiter(options =>
{
    options.AddPolicy("perIp", context =>
    {
        var ip = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        Console.WriteLine($"[RateLimiter] Partition Key: {ip}");

        return RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: ip,
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 100,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 10,
                QueueProcessingOrder = QueueProcessingOrder.OldestFirst
            });
    });
});

// SCALABILITY: Distributed Cache (Redis)
//builder.Services.AddStackExchangeRedisCache(options =>
//{
//    options.Configuration = builder.Configuration.GetConnectionString("Redis")
//        ?? "localhost:6379";
//    options.InstanceName = "SARDemo:";
//});

// AVAILABILITY: Health Checks
builder.Services.AddHealthChecks()
    .AddCheck<DatabaseHealthCheck>("database")
    .AddCheck<ExternalApiHealthCheck>("external_api")
    .AddRedis(
        builder.Configuration.GetConnectionString("Redis") ?? "localhost:6379",
        name: "redis");

builder.Services.AddTransient<FaultInjectingHandler>();


// RESILIENCE: HttpClient با Pipeline (Bulkhead → CB → Retry)
builder.Services.AddHttpClient<IRecommendationService, RecommendationService>(client =>
{
    client.BaseAddress = new Uri("https://jsonplaceholder.typicode.com/");
    client.Timeout = TimeSpan.FromSeconds(5);
})
.AddResilienceHandler("sar-pipeline", resilienceBuilder =>
{
    resilienceBuilder.AddCircuitBreaker(new HttpCircuitBreakerStrategyOptions
    {
        FailureRatio = 1.0,
        MinimumThroughput = 3,
        SamplingDuration = TimeSpan.FromSeconds(30),
        BreakDuration = TimeSpan.FromSeconds(30),
        ShouldHandle = new PredicateBuilder<HttpResponseMessage>()
            .Handle<HttpRequestException>()
            .Handle<TimeoutRejectedException>(),
        OnOpened = args =>
        {
            Console.WriteLine(
                $"[CircuitBreaker] Circuit opened! Error: {args.Outcome.Exception?.Message} - Duration: {args.BreakDuration.TotalSeconds}s");
            return ValueTask.CompletedTask;
        },
        OnClosed = args =>
        {
            Console.WriteLine("[CircuitBreaker] Circuit closed (service is healthy).");
            return ValueTask.CompletedTask;
        },
        OnHalfOpened = args =>
        {
            Console.WriteLine("[CircuitBreaker] Half-open: sending test request...");
            return ValueTask.CompletedTask;
        }
    });

    resilienceBuilder.AddRetry(new HttpRetryStrategyOptions
    {
        MaxRetryAttempts = 3,
        BackoffType = DelayBackoffType.Exponential,
        UseJitter = true,
        Delay = TimeSpan.FromSeconds(2),
        ShouldHandle = new PredicateBuilder<HttpResponseMessage>()
            .Handle<HttpRequestException>()
            .Handle<TimeoutRejectedException>(),
        OnRetry = args =>
        {
            var ex = args.Outcome.Exception;
            Console.WriteLine(
                $"[Retry] Attempt {args.AttemptNumber} after {args.RetryDelay.TotalSeconds}s - {ex?.Message}");
            return ValueTask.CompletedTask;
        }
    });


    resilienceBuilder.AddRateLimiter(new RateLimiterStrategyOptions
    {
        DefaultRateLimiterOptions = new ConcurrencyLimiterOptions
        {
            PermitLimit = 10,
            QueueLimit = 5
        },
        OnRejected = args =>
        {
            Console.WriteLine("[Bulkhead] Request rejected! Capacity is full.");
            return ValueTask.CompletedTask;
        }
    });
});

builder.Services.AddScoped<IOrderService, OrderService>();
builder.Services.AddControllers();

var app = builder.Build();

// AVAILABILITY: Health Check Endpoints
app.MapHealthChecks("/health");
app.MapHealthChecks("/health/ready");
app.MapHealthChecks("/health/live");

// SCALABILITY: Rate Limiting Middleware
app.UseRateLimiter();

// OBSERVABILITY: Correlation ID Middleware
app.Use(async (context, next) =>
{
    var correlationId = context.Request.Headers["X-Correlation-Id"].FirstOrDefault()
                        ?? Guid.NewGuid().ToString("N");
    context.Items["CorrelationId"] = correlationId;
    context.Response.Headers["X-Correlation-Id"] = correlationId;

    var logger = context.RequestServices.GetRequiredService<ILogger<Program>>();
    logger.LogInformation("[CorrelationId: {Id}] Request {Method} {Path}",
        correlationId, context.Request.Method, context.Request.Path);

    await next();
});

app.MapControllers();
app.Run();