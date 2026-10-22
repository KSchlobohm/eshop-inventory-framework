using System.Data.Entity;
using eShopInventoryApi.Models;
using Microsoft.AspNet.Identity.EntityFramework;

namespace eShopInventoryApi.DAL
{
    public class InventoryContext : IdentityDbContext<ApiUser>
    {
        public InventoryContext() : base("name=InventoryContext")
        {
        }

        public DbSet<StockRecord> Stock { get; set; }
    }
}
