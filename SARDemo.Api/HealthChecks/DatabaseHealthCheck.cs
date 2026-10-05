using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace SARDemo.Api.HealthChecks
{
    public class DatabaseHealthCheck : IHealthCheck
    {
        public Task<HealthCheckResult> CheckHealthAsync(
            HealthCheckContext context,
            CancellationToken cancellationToken = default)
        {
            // Simulation: In a real-world scenario, check the database connection.
            var isHealthy = true;

            return Task.FromResult(isHealthy
                ? HealthCheckResult.Healthy("Database is connected.")
                : HealthCheckResult.Unhealthy("Database is unavailable."));
        }
    }

    public class ExternalApiHealthCheck : IHealthCheck
    {
        private readonly IHttpClientFactory _httpClientFactory;

        public ExternalApiHealthCheck(IHttpClientFactory httpClientFactory)
        {
            _httpClientFactory = httpClientFactory;
        }

        public async Task<HealthCheckResult> CheckHealthAsync(
            HealthCheckContext context,
            CancellationToken cancellationToken = default)
        {
            try
            {
                var client = _httpClientFactory.CreateClient();
                client.Timeout = TimeSpan.FromSeconds(2);
                var response = await client.GetAsync("https://jsonplaceholder.typicode.com/posts/1",
                    cancellationToken);

                return response.IsSuccessStatusCode
                    ? HealthCheckResult.Healthy("External service is available.")
                    : HealthCheckResult.Degraded($"External service returned {response.StatusCode}.");
            }
            catch (Exception ex)
            {
                return HealthCheckResult.Unhealthy("External service is unavailable.", ex);
            }
        }
    }
}