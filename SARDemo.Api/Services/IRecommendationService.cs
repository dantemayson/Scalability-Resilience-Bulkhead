namespace SARDemo.Api.Services
{
    public interface IRecommendationService
    {
        Task<string> GetRecommendationsAsync(string customerId);
    }
}
