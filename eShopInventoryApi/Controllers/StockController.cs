using System.Data.Entity;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using System.Web.Http;
using eShopInventoryApi.DAL;
using eShopInventoryApi.Models;

namespace eShopInventoryApi.Controllers
{
    [RoutePrefix("api/stock")]
    public class StockController : ApiController
    {
        private readonly InventoryContext db = new InventoryContext();

        [HttpGet, Route("")]
        public async Task<IHttpActionResult> GetAll()
        {
            return Ok(await db.Stock.OrderBy(s => s.Id).ToListAsync());
        }

        [HttpGet, Route("{id:int}", Name = "GetStockRecord")]
        public async Task<IHttpActionResult> Get(int id)
        {
            var stock = await db.Stock.FindAsync(id);
            if (stock == null)
                return NotFound();
            return Ok(stock);
        }

        [HttpPost, Route("")]
        public async Task<IHttpActionResult> Create(StockRequest request)
        {
            if (request == null)
                return BadRequest("A stock record is required.");
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var stock = new StockRecord { Sku = request.Sku, Quantity = request.Quantity.Value };
            db.Stock.Add(stock);
            await db.SaveChangesAsync();
            return CreatedAtRoute("GetStockRecord", new { id = stock.Id }, stock);
        }

        [HttpPut, Route("{id:int}")]
        public async Task<IHttpActionResult> Update(int id, StockRequest request)
        {
            if (request == null)
                return BadRequest("A stock record is required.");
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var stock = await db.Stock.FindAsync(id);
            if (stock == null)
                return NotFound();

            stock.Sku = request.Sku;
            stock.Quantity = request.Quantity.Value;
            await db.SaveChangesAsync();
            return StatusCode(HttpStatusCode.NoContent);
        }

        [HttpDelete, Route("{id:int}")]
        public async Task<IHttpActionResult> Delete(int id)
        {
            var stock = await db.Stock.FindAsync(id);
            if (stock == null)
                return NotFound();

            db.Stock.Remove(stock);
            await db.SaveChangesAsync();
            return StatusCode(HttpStatusCode.NoContent);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
                db.Dispose();
            base.Dispose(disposing);
        }
    }
}
