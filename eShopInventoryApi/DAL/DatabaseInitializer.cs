using System;
using System.Data.Entity;
using eShopInventoryApi.App_Start;
using eShopInventoryApi.Models;
using eShopInventoryApi.Security;
using Microsoft.AspNet.Identity;

namespace eShopInventoryApi.DAL
{
    public class DatabaseInitializer : CreateDatabaseIfNotExists<InventoryContext>
    {
        private readonly AuthenticationSettings settings;

        public DatabaseInitializer(AuthenticationSettings settings)
        {
            this.settings = settings;
        }

        protected override void Seed(InventoryContext context)
        {
            using (var manager = IdentityConfig.CreateUserManager(context))
            {
                var result = manager.Create(new ApiUser
                {
                    UserName = settings.SeedUserName
                }, settings.SeedPassword);
                if (!result.Succeeded)
                {
                    throw new InvalidOperationException(
                        "Could not create the seed user: " + string.Join("; ", result.Errors));
                }
            }
            context.Stock.AddRange(new[]
            {
                new StockRecord { Sku = "MUG-001", Quantity = 25 },
                new StockRecord { Sku = "TEE-001", Quantity = 40 },
                new StockRecord { Sku = "BAG-001", Quantity = 15 }
            });
            context.SaveChanges();
        }
    }
}
