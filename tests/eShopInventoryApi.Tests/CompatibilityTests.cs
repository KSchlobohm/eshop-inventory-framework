using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel.DataAnnotations;
using System.Configuration;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using eShopInventoryApi.App_Start;
using eShopInventoryApi.Controllers;
using eShopInventoryApi.Models;
using eShopInventoryApi.Security;
using Microsoft.AspNet.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Serialization;

namespace eShopInventoryApi.Tests
{
    [TestClass]
    public class CompatibilityTests
    {
        private static NameValueCollection ValidSettings()
        {
            return new NameValueCollection
            {
                { "JwtSigningKey", Convert.ToBase64String(new byte[32]) },
                { "JwtIssuer", "urn:test:inventory" },
                { "JwtAudience", "test-inventory" },
                { "SeedUserName", "merchant.test" },
                { "SeedPassword", "abcdef" }
            };
        }

        [TestMethod]
        public void LegacyPasswordHasherVerifiesItsIdentityV2Hash()
        {
            var hasher = new PasswordHasher();
            var hash = hasher.HashPassword("test-only-password");
            Assert.AreEqual(0, Convert.FromBase64String(hash)[0]);
            Assert.AreEqual(PasswordVerificationResult.Success, hasher.VerifyHashedPassword(hash, "test-only-password"));
            Assert.AreEqual(PasswordVerificationResult.Failed, hasher.VerifyHashedPassword(hash, "wrong"));
        }

        [TestMethod]
        public async Task LegacyPasswordPolicyKeepsSixCharacterMinimum()
        {
            var validator = IdentityConfig.CreatePasswordValidator();
            Assert.IsFalse((await validator.ValidateAsync("abcde")).Succeeded);
            Assert.IsTrue((await validator.ValidateAsync("abcdef")).Succeeded);
        }

        [TestMethod]
        public void ValidSettingsPreserveValuesAndJwtPolicy()
        {
            var settings = AuthenticationSettings.Load(ValidSettings());
            Assert.AreEqual("merchant.test", settings.SeedUserName);
            Assert.AreEqual(256, settings.SigningKey.KeySize);
            var policy = settings.CreateTokenValidationParameters();
            Assert.IsTrue(policy.ValidateIssuer && policy.ValidateAudience && policy.ValidateIssuerSigningKey);
            Assert.IsTrue(policy.ValidateLifetime && policy.RequireExpirationTime && policy.RequireSignedTokens);
            Assert.AreEqual(TimeSpan.FromSeconds(30), policy.ClockSkew);
            CollectionAssert.AreEqual(new[] { SecurityAlgorithms.HmacSha256 }, policy.ValidAlgorithms.ToArray());
        }

        [TestMethod]
        [DataRow("JwtSigningKey", null)]
        [DataRow("JwtSigningKey", " ")]
        [DataRow("JwtSigningKey", "not-base64")]
        [DataRow("JwtSigningKey", "AA==")]
        [DataRow("JwtIssuer", null)]
        [DataRow("JwtAudience", " ")]
        [DataRow("SeedUserName", null)]
        [DataRow("SeedPassword", null)]
        [DataRow("SeedPassword", "abcde")]
        public void InvalidSettingsFailExplicitly(string key, string value)
        {
            var values = ValidSettings();
            values[key] = value;
            Assert.ThrowsExactly<ConfigurationErrorsException>(() => AuthenticationSettings.Load(values));
        }

        [TestMethod]
        public void SeedUserNameBoundaryIsPreserved()
        {
            var values = ValidSettings();
            values["SeedUserName"] = new string('u', 256);
            Assert.AreEqual(256, AuthenticationSettings.Load(values).SeedUserName.Length);
            values["SeedUserName"] += "u";
            Assert.ThrowsExactly<ConfigurationErrorsException>(() => AuthenticationSettings.Load(values));
        }

        [TestMethod]
        [DataRow(null, null, 2)]
        [DataRow("", -1, 2)]
        [DataRow("valid", 0, 0)]
        [DataRow("valid", int.MaxValue, 0)]
        public void StockAnnotationsPreserveValidation(string sku, int? quantity, int expectedErrors)
        {
            var errors = new List<ValidationResult>();
            var request = new StockRequest { Sku = sku, Quantity = quantity };
            Validator.TryValidateObject(request, new ValidationContext(request), errors, true);
            Assert.HasCount(expectedErrors, errors);
        }

        [TestMethod]
        public void StringLengthBoundariesArePreserved()
        {
            foreach (var length in new[] { 64, 65 })
            {
                var stock = new StockRequest { Sku = new string('s', length), Quantity = 0 };
                Assert.AreEqual(length == 64, Validator.TryValidateObject(stock, new ValidationContext(stock), null, true));
            }
            foreach (var length in new[] { 256, 257 })
            {
                var login = new LoginRequest { UserName = new string('u', length), Password = "abcdef" };
                Assert.AreEqual(length == 256, Validator.TryValidateObject(login, new ValidationContext(login), null, true));
            }
        }

        [TestMethod]
        public async Task ValidationEnvelopeKeepsLegacyKeysAndArrays()
        {
            using (var controller = new StockController())
            {
                controller.ModelState.AddModelError("Sku", "The Sku field is required.");
                controller.ModelState.AddModelError("Quantity", "The Quantity field is required.");
                var response = (BadRequestObjectResult)await controller.Create(new StockRequest());
                var json = JObject.FromObject(response.Value);
                Assert.AreEqual("The request is invalid.", (string)json["Message"]);
                Assert.AreEqual("The Sku field is required.", (string)json["ModelState"]["request.Sku"][0]);
                Assert.AreEqual("The Quantity field is required.", (string)json["ModelState"]["request.Quantity"][0]);
                Assert.HasCount(2, json.Properties());
            }
        }

        [TestMethod]
        public async Task NullStockUsesMessageRatherThanProblemDetails()
        {
            using (var controller = new StockController())
            {
                var response = (BadRequestObjectResult)await controller.Create(null);
                Assert.AreEqual("{\"Message\":\"A stock record is required.\"}", JsonConvert.SerializeObject(response.Value));
            }
        }

        [TestMethod]
        public void NewtonsoftPreservesPascalCaseAndExplicitSnakeCase()
        {
            var serializer = new JsonSerializerSettings { ContractResolver = new DefaultContractResolver() };
            Assert.AreEqual("{\"Id\":1,\"Sku\":\"MUG-001\",\"Quantity\":25}",
                JsonConvert.SerializeObject(new StockRecord { Id = 1, Sku = "MUG-001", Quantity = 25 }, serializer));
            Assert.AreEqual("{\"access_token\":\"test\",\"token_type\":\"Bearer\",\"expires_in\":1800}",
                JsonConvert.SerializeObject(new { access_token = "test", token_type = "Bearer", expires_in = 1800 }, serializer));
        }

        [TestMethod]
        [DataRow("valid", true)]
        [DataRow("within-skew", true)]
        [DataRow("expired", false)]
        [DataRow("issuer", false)]
        [DataRow("audience", false)]
        [DataRow("signature", false)]
        [DataRow("algorithm", false)]
        [DataRow("missing-expiry", false)]
        public void JwtValidationUsesTheApplicationPolicy(string defect, bool expectedValid)
        {
            var settings = AuthenticationSettings.Load(ValidSettings());
            var now = DateTime.UtcNow;
            var key = defect == "algorithm" ? new SymmetricSecurityKey(new byte[64]) :
                defect == "signature" ? new SymmetricSecurityKey(Enumerable.Repeat((byte)1, 32).ToArray()) : settings.SigningKey;
            var jwt = new JwtSecurityToken(
                defect == "issuer" ? "wrong" : settings.Issuer,
                defect == "audience" ? "wrong" : settings.Audience,
                new[] { new Claim(JwtRegisteredClaimNames.Sub, "legacy-user"), new Claim(ClaimTypes.Name, "merchant") },
                now.AddMinutes(-10),
                defect == "missing-expiry" ? null : defect == "expired" ? now.AddSeconds(-120) :
                    defect == "within-skew" ? now.AddSeconds(-10) : now.AddMinutes(30),
                new SigningCredentials(key, defect == "algorithm" ? SecurityAlgorithms.HmacSha512 : SecurityAlgorithms.HmacSha256));
            var handler = new JwtSecurityTokenHandler();
            var token = handler.WriteToken(jwt);
            if (expectedValid)
            {
                var principal = handler.ValidateToken(token, settings.CreateTokenValidationParameters(), out _);
                Assert.AreEqual("merchant", principal.Identity.Name);
                Assert.AreEqual("legacy-user", principal.FindFirst(ClaimTypes.NameIdentifier).Value);
            }
            else
            {
                Assert.Throws<SecurityTokenException>(() => handler.ValidateToken(token, settings.CreateTokenValidationParameters(), out _));
            }
        }
    }
}
