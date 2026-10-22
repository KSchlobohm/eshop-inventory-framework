using System.ComponentModel.DataAnnotations;

namespace eShopInventoryApi.Models
{
    public class StockRecord
    {
        public int Id { get; set; }

        [Required, StringLength(64)]
        public string Sku { get; set; }

        public int Quantity { get; set; }
    }
}
