using System;
using System.Configuration;
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
            byte[] key;
            try
            {
                key = Convert.FromBase64String(Required("JwtSigningKey"));
            }
            catch (FormatException ex)
            {
                throw new ConfigurationErrorsException("JwtSigningKey must be Base64 encoded.", ex);
            }

            if (key.Length < 32)
            {
                throw new ConfigurationErrorsException("JwtSigningKey must contain at least 32 random bytes.");
            }

            return new AuthenticationSettings
            {
                Issuer = Required("JwtIssuer"),
                Audience = Required("JwtAudience"),
                SigningKey = new SymmetricSecurityKey(key),
                SeedUserName = Required("SeedUserName"),
                SeedPassword = Required("SeedPassword")
            };
        }

        private static string Required(string name)
        {
            var value = ConfigurationManager.AppSettings[name];
            if (string.IsNullOrWhiteSpace(value))
            {
                throw new ConfigurationErrorsException(
                    "Set " + name + " in AppSettings.local.config before starting the Inventory API.");
            }
            return value;
        }
    }
}
