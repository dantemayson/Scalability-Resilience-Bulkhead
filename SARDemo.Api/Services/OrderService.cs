using Polly.Bulkhead;
using Polly.CircuitBreaker;
using SARDemo.Api.Models;
using System.Collections.Concurrent;

namespace SARDemo.Api.Services
{
    public class OrderService : IOrderService
    {
        // In the real world: a database. Here, in-memory for simplicity.
        private static readonly ConcurrentDictionary<int, Order> _orders = new();
        private static int _nextId = 1;
        private readonly IRecommendationService _recommendationService;
        private readonly ILogger<OrderService> _logger;

        public OrderService(IRecommendationService recommendationService,
                            ILogger<OrderService> logger)
        {
            _recommendationService = recommendationService;
            _logger = logger;
        }

        public async Task<Order> CreateOrderAsync(CreateOrderRequest request, string correlationId)
        {
            var order = new Order
            {
                Id = Interlocked.Increment(ref _nextId),
                CustomerId = request.CustomerId,
                Amount = request.Amount
            };

            _orders[order.Id] = order;

            _logger.LogInformation("[CorrelationId: {Id}] Order {OrderId} created",
                correlationId, order.Id);

            try
            {
                var recommendations = await _recommendationService
                    .GetRecommendationsAsync(request.CustomerId);
                _logger.LogInformation("[CorrelationId: {Id}] Recommender replied",
                    correlationId);
            }
            catch (BrokenCircuitException ex)
            {
                _logger.LogWarning("[CorrelationId: {Id}] Circuit is open: {Msg}",
                    correlationId, ex.Message);
            }
            catch (BulkheadRejectedException ex)
            {
                _logger.LogWarning("[CorrelationId: {Id}] Bulkhead rejected: {Msg}",
                    correlationId, ex.Message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[CorrelationId: {Id}] Recommender error",
                    correlationId);
            }

            return order;
        }

        public Task<Order?> GetOrderAsync(int id)
        {
            _orders.TryGetValue(id, out var order);
            return Task.FromResult(order);
        }
    }
}
