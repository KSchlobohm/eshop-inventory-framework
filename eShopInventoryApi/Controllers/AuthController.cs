using System;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using eShopInventoryApi.App_Start;
using eShopInventoryApi.DAL;
using eShopInventoryApi.Models;
using eShopInventoryApi.Security;
using Microsoft.AspNet.Identity;
using Microsoft.IdentityModel.Tokens;

namespace eShopInventoryApi.Controllers
{
    [Route("api/auth")]
    public class AuthController : InventoryController
    {
        private readonly InventoryContext db = new InventoryContext();
        private readonly UserManager<ApiUser> userManager;

        public AuthController()
        {
            userManager = IdentityConfig.CreateUserManager(db);
        }

        [AllowAnonymous, HttpPost, Route("login")]
        public async Task<IActionResult> Login([FromBody] LoginRequest request)
        {
            if (request == null)
                return BadRequest(new { Message = "A login request is required." });
            if (!ModelState.IsValid)
                return InvalidRequest();

            var user = await userManager.FindAsync(request.UserName, request.Password);
            if (user == null)
            {
                return Unauthorized();
            }

            var settings = AuthenticationSettings.Load();
            var now = DateTime.UtcNow;
            var token = new JwtSecurityToken(
                issuer: settings.Issuer,
                audience: settings.Audience,
                claims: new[]
                {
                    new Claim(JwtRegisteredClaimNames.Sub, user.Id),
                    new Claim(ClaimTypes.Name, user.UserName)
                },
                notBefore: now,
                expires: now.AddMinutes(30),
                signingCredentials: new SigningCredentials(settings.SigningKey, SecurityAlgorithms.HmacSha256));

            return Ok(new
            {
                access_token = new JwtSecurityTokenHandler().WriteToken(token),
                token_type = "Bearer",
                expires_in = 1800
            });
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                userManager.Dispose();
                db.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
