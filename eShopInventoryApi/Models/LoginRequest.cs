using System.ComponentModel.DataAnnotations;

namespace eShopInventoryApi.Models
{
    public class LoginRequest
    {
        [Required, StringLength(100)]
        public string UserName { get; set; }

        [Required]
        public string Password { get; set; }
    }
}
