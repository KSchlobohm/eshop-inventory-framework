using eShopInventoryApi.DAL;
using eShopInventoryApi.Models;
using Microsoft.AspNet.Identity;
using Microsoft.AspNet.Identity.EntityFramework;

namespace eShopInventoryApi.App_Start
{
    public static class IdentityConfig
    {
        public static UserManager<ApiUser> CreateUserManager(InventoryContext context)
        {
            var store = new UserStore<ApiUser>(context) { DisposeContext = false };
            var manager = new UserManager<ApiUser>(store);
            manager.UserValidator = new UserValidator<ApiUser>(manager)
            {
                AllowOnlyAlphanumericUserNames = false
            };
            manager.PasswordValidator = CreatePasswordValidator();
            return manager;
        }

        public static PasswordValidator CreatePasswordValidator()
        {
            return new PasswordValidator { RequiredLength = 6 };
        }
    }
}
