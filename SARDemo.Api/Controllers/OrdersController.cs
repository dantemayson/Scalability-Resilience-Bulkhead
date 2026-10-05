using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using SARDemo.Api.Models;
using SARDemo.Api.Services;

namespace SARDemo.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [EnableRateLimiting("perIp")]
    public class OrdersController : ControllerBase
    {
        private readonly IOrderService _orderService;
        private readonly ILogger<OrdersController> _logger;

        public OrdersController(IOrderService orderService,
                                ILogger<OrdersController> logger)
        {
            _orderService = orderService;
            _logger = logger;
        }

        [HttpPost]
        public async Task<IActionResult> Create([FromBody] CreateOrderRequest request)
        {
            var correlationId = HttpContext.Items["CorrelationId"]?.ToString() ?? "unknown";

            // SCALABILITY: Check Cache before heavy processing
            var order = await _orderService.CreateOrderAsync(request, correlationId);

            // AVAILABILITY: Return 201 even if the downstream service failed
            return CreatedAtAction(nameof(Get), new { id = order.Id }, order);
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> Get(int id)
        {
            var order = await _orderService.GetOrderAsync(id);
            if (order is null) return NotFound();
            return Ok(order);
        }

        // Simulate random failure for testing Resilience
        [HttpGet("simulate-failure")]
        public IActionResult SimulateFailure()
        {
            throw new HttpRequestException("Simulated failure for testing Circuit Breaker");
        }
    }
}