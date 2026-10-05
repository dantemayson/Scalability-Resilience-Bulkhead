using SARDemo.Api.Models;

namespace SARDemo.Api.Services
{
    public interface IOrderService
    {
        Task<Order> CreateOrderAsync(CreateOrderRequest request, string correlationId);
        Task<Order?> GetOrderAsync(int id);
    }
}
