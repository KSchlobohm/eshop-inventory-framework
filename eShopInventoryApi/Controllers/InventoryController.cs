using System.Linq;
using Microsoft.AspNetCore.Mvc;

namespace eShopInventoryApi.Controllers
{
    public abstract class InventoryController : Controller
    {
        protected IActionResult InvalidRequest()
        {
            return BadRequest(new
            {
                Message = "The request is invalid.",
                ModelState = ModelState.Where(entry => entry.Value.Errors.Count > 0)
                    .ToDictionary(entry => entry.Key.StartsWith("request.") ? entry.Key : "request." + entry.Key,
                        entry => entry.Value.Errors.Select(error => error.ErrorMessage).ToArray())
            });
        }
    }
}
