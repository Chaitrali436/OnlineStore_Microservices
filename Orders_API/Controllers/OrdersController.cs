using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Orders_API.Models;
using Orders_API.Services;

namespace Orders_API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class OrdersController(OrderService orders) : ControllerBase
    {
        [HttpGet]
        public async Task<IActionResult> GetAll() => Ok(await orders.GetAllAsync());

        [HttpGet("{id:int}")]
        public async Task<IActionResult> GetById(int id)
        {
            var order = await orders.GetByIdAsync(id);
            return order is null ? NotFound() : Ok(order);
        }

        [HttpPost]
        public async Task<IActionResult> Create(CreateOrderRequest request, CancellationToken ct)
        {
            var result = await orders.PlaceOrderAsync(request, ct);

            return result.Order is not null
                ? AcceptedAtAction(nameof(GetById), new { id = result.Order.Id },
                new { orderId = result.Order.Id, status = result.Order.Status.ToString() })
                : StatusCode(result.StatusCode, new { error = result.Error });              
                
        }
    }
}
