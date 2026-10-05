namespace SARDemo.Api.Models
{
    public class Order
    {
        public int Id { get; set; }
        public string CustomerId { get; set; } = string.Empty;
        public decimal Amount { get; set; }
        public string Status { get; set; } = "Pending";
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
