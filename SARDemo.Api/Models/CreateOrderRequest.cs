namespace SARDemo.Api.Models
{
    public class CreateOrderRequest
    {
        public string CustomerId { get; set; } = string.Empty;
        public decimal Amount { get; set; }
    }
}
