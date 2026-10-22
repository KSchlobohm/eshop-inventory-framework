using System.ComponentModel.DataAnnotations;

namespace eShopInventoryApi.Models
{
    public class StockRequest
    {
        [Required, StringLength(64)]
        public string Sku { get; set; }

        [Required, Range(0, int.MaxValue)]
        public int? Quantity { get; set; }
    }
}
