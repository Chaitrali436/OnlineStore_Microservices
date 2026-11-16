using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Products_Api.Data;
using Products_Api.Models;

namespace Products_Api.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class ProductsController(ProductsDbContext db) : ControllerBase
    {
        [HttpGet]
        public async Task<IActionResult> GetAll() =>
        Ok(await db.Products
            .Select(p => new ProductDto(p.Id, p.Name, p.Price, p.Stock)).ToListAsync());
           

        [HttpGet("{id:int}")]
        public async Task<IActionResult> GetById(int id)
        {
            var p = await db.Products.FindAsync(id);
            return p is null ? NotFound() : Ok(new ProductDto(p.Id, p.Name, p.Price, p.Stock));
        }

        [HttpPost]
        public async Task<IActionResult> Create(CreateProductRequest request)
        {
            var product = new Product { Name = request.Name, Price = request.Price, Stock = request.Stock };
            db.Products.Add(product);
            await db.SaveChangesAsync();
            var dto = new ProductDto(product.Id, product.Name, product.Price, product.Stock);
            return CreatedAtAction(nameof(GetById), new { id = product.Id }, dto);
        }

        [HttpPost("{id:int}/reduce-stock")]
        public async Task<IActionResult> ReduceStock(int id, ReduceStockRequest request)
        {
            if (request.Quantity <= 0) return BadRequest(new { error = "Quantity must be greater than zero." });

            var product = await db.Products.FindAsync(id);
            if (product is null) return NotFound();
            if (product.Stock < request.Quantity) return Conflict(new { error = "Not enough stock." });

            product.Stock -= request.Quantity;
            await db.SaveChangesAsync();
            return Ok(new ProductDto(product.Id, product.Name, product.Price, product.Stock));
        }
    }
}
