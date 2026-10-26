using System.Data.Entity;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using eShopInventoryApi.DAL;
using eShopInventoryApi.Models;

namespace eShopInventoryApi.Controllers
{
    [Route("api/stock")]
    public class StockController : InventoryController
    {
        private readonly InventoryContext db = new InventoryContext();

        [HttpGet, Route("")]
        public async Task<IActionResult> GetAll()
        {
            return Ok(await db.Stock.OrderBy(s => s.Id).ToListAsync());
        }

        [HttpGet, Route("{id:int}", Name = "GetStockRecord")]
        public async Task<IActionResult> Get(int id)
        {
            var stock = await db.Stock.FindAsync(id);
            if (stock == null)
                return NotFound();
            return Ok(stock);
        }

        [HttpPost, Route("")]
        public async Task<IActionResult> Create([FromBody] StockRequest request)
        {
            if (request == null)
                return BadRequest(new { Message = "A stock record is required." });
            if (!ModelState.IsValid)
                return InvalidRequest();

            var stock = new StockRecord { Sku = request.Sku, Quantity = request.Quantity.Value };
            db.Stock.Add(stock);
            await db.SaveChangesAsync();
            return CreatedAtRoute("GetStockRecord", new { id = stock.Id }, stock);
        }

        [HttpPut, Route("{id:int}")]
        public async Task<IActionResult> Update(int id, [FromBody] StockRequest request)
        {
            if (request == null)
                return BadRequest(new { Message = "A stock record is required." });
            if (!ModelState.IsValid)
                return InvalidRequest();

            var stock = await db.Stock.FindAsync(id);
            if (stock == null)
                return NotFound();

            stock.Sku = request.Sku;
            stock.Quantity = request.Quantity.Value;
            try
            {
                await db.SaveChangesAsync();
            }
            catch (System.Data.Entity.Infrastructure.DbUpdateConcurrencyException)
            {
                return NotFound();
            }
            return NoContent();
        }

        [HttpDelete, Route("{id:int}")]
        public async Task<IActionResult> Delete(int id)
        {
            var stock = await db.Stock.FindAsync(id);
            if (stock == null)
                return NotFound();

            db.Stock.Remove(stock);
            try
            {
                await db.SaveChangesAsync();
            }
            catch (System.Data.Entity.Infrastructure.DbUpdateConcurrencyException)
            {
                return NotFound();
            }
            return NoContent();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
                db.Dispose();
            base.Dispose(disposing);
        }
    }
}
