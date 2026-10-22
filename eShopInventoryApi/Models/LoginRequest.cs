using System.ComponentModel.DataAnnotations;

namespace eShopInventoryApi.Models
{
    public class LoginRequest
    {
        [Required, StringLength(256)]
        public string UserName { get; set; }

        [Required]
        public string Password { get; set; }
    }
}
