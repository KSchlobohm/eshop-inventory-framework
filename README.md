# eShop Inventory on .NET Framework

A lean stock management API sample built with ASP.NET Web API 2 on
.NET Framework 4.8, Entity Framework 6, and SQL Server LocalDB.

The sample provides stock CRUD, ASP.NET Identity user management, and JWT bearer authentication,
without Swagger. It represents a classic application for evaluating
.NET migration tooling.

Stock records contain `Id`, `Sku`, and `Quantity`. The API provides five
stock operations and an anonymous login endpoint that issues 30-minute JWTs.
See the [glossary](GLOSSARY.md) for terminology.

## Run locally

Use Windows with Visual Studio 2019/2022, ASP.NET web development tooling,
the .NET Framework 4.8 targeting pack, and SQL Server LocalDB installed.

1. Open `eShopInventory.sln` and restore NuGet packages.
2. Copy `eShopInventoryApi\AppSettings.local.config.example` to
   `eShopInventoryApi\AppSettings.local.config`. Set `SeedPassword` to a local
   password of at least six characters and `JwtSigningKey` to a separately generated Base64 key containing
   at least 32 random bytes. `SeedUserName` defaults to `merchant`.
3. Build the solution and start `eShopInventoryApi` with IIS Express at
   `http://localhost:61462`.
4. Open `eShopInventoryApi\Inventory.http` in an HTTP request client.
   Set its local password, sign in, and paste the returned `access_token`
   into its `token` variable before sending stock requests.

Generate a signing key in PowerShell:

```powershell
$key = New-Object byte[] 32
$rng = [System.Security.Cryptography.RandomNumberGenerator]::Create()
$rng.GetBytes($key)
$rng.Dispose()
[Convert]::ToBase64String($key)
```

Local settings are ignored by Git. Missing required settings fail startup.
On first startup, Entity Framework creates the `eShopInventoryApi` database
with three stock records and a user created through ASP.NET Identity's
`UserManager`. Identity stores the hashed password in its standard EF-backed
user store.
The database is preserved thereafter; changing seed credentials in the local
settings does not update an existing user.

The initializer does not migrate or drop existing databases. If an older
sample schema already exists, choose a fresh database name in the
`InventoryContext` connection string in `Web.config`.

## API

| Method | Route | Purpose |
| --- | --- | --- |
| POST | `/api/auth/login` | Sign in anonymously with `UserName` and `Password`. |
| GET | `/api/stock` | List stock. |
| GET | `/api/stock/{id}` | Read one stock record. |
| POST | `/api/stock` | Create a stock record. |
| PUT | `/api/stock/{id}` | Update a stock record. |
| DELETE | `/api/stock/{id}` | Delete a stock record. |

All stock routes require `Authorization: Bearer <token>`. Create and update
accept `Sku` and a nonnegative `Quantity`. Create returns `201`; update and
delete return `204`. Invalid input returns `400`, missing records return `404`,
and missing, invalid, or expired tokens return `401`.

`Inventory.http` contains manual request examples, not automated tests.
This sample has no Swagger, frontend, integrations, roles, registration,
refresh tokens, migrations, or test suites. Local HTTP is for development;
this is not a production deployment template.

## License

[MIT](LICENSE).
