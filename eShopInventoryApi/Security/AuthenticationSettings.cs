using System;
using System.Collections.Specialized;
using System.Configuration;
using eShopInventoryApi.App_Start;
using Microsoft.IdentityModel.Tokens;

namespace eShopInventoryApi.Security
{
    public class AuthenticationSettings
    {
        public string Issuer { get; private set; }
        public string Audience { get; private set; }
        public SymmetricSecurityKey SigningKey { get; private set; }
        public string SeedUserName { get; private set; }
        public string SeedPassword { get; private set; }

        public static AuthenticationSettings Load()
        {
            return Load(ConfigurationManager.AppSettings);
        }

        public static AuthenticationSettings Load(NameValueCollection values)
        {
            byte[] key;
            try
            {
                key = Convert.FromBase64String(Required(values, "JwtSigningKey"));
            }
            catch (FormatException ex)
            {
                throw new ConfigurationErrorsException("JwtSigningKey must be Base64 encoded.", ex);
            }

            if (key.Length < 32)
            {
                throw new ConfigurationErrorsException("JwtSigningKey must contain at least 32 random bytes.");
            }

            var seedPassword = Required(values, "SeedPassword");
            var passwordResult = IdentityConfig.CreatePasswordValidator()
                .ValidateAsync(seedPassword).GetAwaiter().GetResult();
            if (!passwordResult.Succeeded)
            {
                throw new ConfigurationErrorsException(
                    "Invalid SeedPassword in AppSettings.local.config: " +
                    string.Join("; ", passwordResult.Errors));
            }

            var seedUserName = Required(values, "SeedUserName");
            if (seedUserName.Length > 256)
            {
                throw new ConfigurationErrorsException(
                    "SeedUserName in AppSettings.local.config must be at most 256 characters.");
            }

            return new AuthenticationSettings
            {
                Issuer = Required(values, "JwtIssuer"),
                Audience = Required(values, "JwtAudience"),
                SigningKey = new SymmetricSecurityKey(key),
                SeedUserName = seedUserName,
                SeedPassword = seedPassword
            };
        }

        public TokenValidationParameters CreateTokenValidationParameters()
        {
            return new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidIssuer = Issuer,
                ValidateAudience = true,
                ValidAudience = Audience,
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = SigningKey,
                ValidateLifetime = true,
                RequireExpirationTime = true,
                RequireSignedTokens = true,
                ValidAlgorithms = new[] { SecurityAlgorithms.HmacSha256 },
                ClockSkew = TimeSpan.FromSeconds(30)
            };
        }

        private static string Required(NameValueCollection values, string name)
        {
            var value = values[name];
            if (string.IsNullOrWhiteSpace(value))
            {
                throw new ConfigurationErrorsException(
                    "Set " + name + " in AppSettings.local.config before starting the Inventory API.");
            }
            return value;
        }
    }
}
