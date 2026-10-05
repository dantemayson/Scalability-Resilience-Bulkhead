using Polly;
using Polly.Retry;
using Polly.CircuitBreaker;

namespace SARDemo.Api.Services;

public class RecommendationService : IRecommendationService
{
    private readonly HttpClient _httpClient;
    private readonly ResiliencePipeline<HttpResponseMessage> _pipeline;
    private readonly Random _random = new();

    public RecommendationService(HttpClient httpClient)
    {
        _httpClient = httpClient;

        _pipeline = new ResiliencePipelineBuilder<HttpResponseMessage>()
            .AddRetry(new RetryStrategyOptions<HttpResponseMessage>
            {
                MaxRetryAttempts = 3,
                BackoffType = DelayBackoffType.Exponential,
                Delay = TimeSpan.FromSeconds(2),
                UseJitter = true,
                ShouldHandle = new PredicateBuilder<HttpResponseMessage>()
                    .Handle<HttpRequestException>(),
                OnRetry = args =>
                {
                    Console.WriteLine($"[Retry] Attempt {args.AttemptNumber} after {args.RetryDelay.TotalSeconds}s - {args.Outcome.Exception?.Message}");
                    return ValueTask.CompletedTask;
                }
            })
            .AddCircuitBreaker(new CircuitBreakerStrategyOptions<HttpResponseMessage>
            {
                FailureRatio = 1.0,
                MinimumThroughput = 3,
                SamplingDuration = TimeSpan.FromSeconds(30),
                BreakDuration = TimeSpan.FromSeconds(30),
                ShouldHandle = new PredicateBuilder<HttpResponseMessage>()
                    .Handle<HttpRequestException>(),
                OnOpened = args =>
                {
                    Console.WriteLine($"[CircuitBreaker] Circuit opened! Error: {args.Outcome.Exception?.Message}");
                    return ValueTask.CompletedTask;
                },
                OnClosed = args =>
                {
                    Console.WriteLine("[CircuitBreaker] Circuit closed.");
                    return ValueTask.CompletedTask;
                },
                OnHalfOpened = args =>
                {
                    Console.WriteLine("[CircuitBreaker] Half-open...");
                    return ValueTask.CompletedTask;
                }
            })
            .Build();
    }

    public async Task<string> GetRecommendationsAsync(string customerId)
    {
        // Delay for Bulkhead
        await Task.Delay(TimeSpan.FromSeconds(2));

        //  Failure occurs inside the Pipeline
        var response = await _pipeline.ExecuteAsync(async ct =>
        {
            // Simulate failure (30%)
            if (_random.Next(1, 10) <= 3)
            {
                throw new HttpRequestException("The recommendation service is temporarily unavailable.");
            }

            return await _httpClient.GetAsync("posts/1", ct);
        });

        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStringAsync();
    }
}