using System;
using System.Data.Entity;
using eShopInventoryApi.DAL;
using eShopInventoryApi.Security;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

namespace eShopInventoryApi
{
    public class Program
    {
        public static void Main(string[] args)
        {
            AppContext.SetData("APP_CONFIG_FILE", typeof(Program).Assembly.Location + ".config");
            var settings = AuthenticationSettings.Load();
            Database.SetInitializer(new DatabaseInitializer(settings));
            using (var db = new InventoryContext())
            {
                db.Database.Initialize(false);
            }

            var builder = WebApplication.CreateBuilder(args);
            builder.Services.AddControllers(options =>
            {
                options.SuppressImplicitRequiredAttributeForNonNullableReferenceTypes = true;
                options.OutputFormatters.RemoveType<Microsoft.AspNetCore.Mvc.Formatters.HttpNoContentOutputFormatter>();
            }).AddNewtonsoftJson(options =>
                options.SerializerSettings.ContractResolver = new DefaultContractResolver());
            builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
                .AddJwtBearer(options =>
                {
                    options.TokenValidationParameters = settings.CreateTokenValidationParameters();
                    options.Events = new JwtBearerEvents
                    {
                        OnChallenge = context =>
                        {
                            context.HandleResponse();
                            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                            context.Response.Headers.WWWAuthenticate = "Bearer";
                            context.Response.ContentType = "application/json; charset=utf-8";
                            return context.Response.WriteAsync(JsonConvert.SerializeObject(new
                            {
                                Message = "Authorization has been denied for this request."
                            }));
                        }
                    };
                });
            builder.Services.AddAuthorization(options =>
                options.FallbackPolicy = new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build());
            var app = builder.Build();
            app.UseAuthentication();
            app.UseAuthorization();
            app.MapControllers();
            app.Run();
        }
    }
}
