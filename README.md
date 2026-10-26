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

Local settings are ignored by Git. Missing required settings or an invalid
seed password fail startup before database initialization.
On first startup, Entity Framework creates the `eShopInventoryApi` database
with three stock records and a user created through ASP.NET Identity's
`UserManager`. Identity stores the hashed password in its standard EF-backed
user store.
The database is preserved thereafter; changing seed credentials in the local
settings does not update an existing user.

The initializer does not migrate or drop existing databases. If an older
sample schema already exists or an earlier failed seed left a database without
a user, choose a fresh database name in the `InventoryContext` connection string
in `Web.config` after correcting the settings. Restarting does not rerun seeding
for an existing database.

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
refresh tokens, or migrations. Local HTTP is for development;
this is not a production deployment template.

## API characterization smoke suite

The separately runnable approval gate in `tests\smoke` exercises the real
application, not mocks or fast unit tests. It covers login, anonymous and
invalid-token rejection, stock CRUD and persisted effects, validation errors,
JSON casing/types, headers, and JSON responses when XML is requested.
`Inventory.http` remains available for manual exploration.

Use Windows, PowerShell 7, SQLCMD, and an application host you started yourself.
Use a dedicated named LocalDB instance and database whose names start with
`eShopSmoke_` (or the retained migration fixture's `eShopBaseline_`).
Never point this suite at a personal database. Configure `InventoryContext`
in the host's `Web.config` (or `ConnectionStrings.InventoryContext` in
`appsettings.json`) to the exact isolated connection string, using integrated
security. Do not override that connection through other configuration sources.
The suite verifies the process command line, port ownership (including
HTTP.sys for IIS Express), configured connection, and database identity before
making HTTP requests. Start the host with its absolute application directory
in its command line and wait for it to be ready.

Set these environment variables in your local PowerShell session, without
printing secret values or passing passwords on command lines:

| Variable | Input |
| --- | --- |
| `INVENTORY_SMOKE_APP_ROOT` | Absolute application directory of your running host. |
| `INVENTORY_SMOKE_HOST_PID` | PID of that host, not the shell or HTTP.sys PID. |
| `INVENTORY_SMOKE_CONNECTION_STRING` | Exact configured isolated LocalDB connection string. |
| `INVENTORY_SMOKE_USERNAME` | Existing fixture Identity username. |
| `INVENTORY_SMOKE_PASSWORD` | That user's password. |
| `INVENTORY_SMOKE_SIGNING_KEY` | Host's Base64 JWT signing key, at least 32 bytes. |
| `INVENTORY_SMOKE_ISSUER` | Host's configured JWT issuer. |
| `INVENTORY_SMOKE_AUDIENCE` | Host's configured JWT audience. |

If username, password, or signing key is absent, the suite prompts for it;
password and signing-key input is masked. For unattended runs, populate all
variables from your ignored local settings or a local secret store. For
example, in the same PowerShell session, load the existing ignored settings
without displaying their values:

```powershell
[xml]$local = Get-Content .\eShopInventoryApi\AppSettings.local.config -Raw
[xml]$web = Get-Content .\eShopInventoryApi\Web.config -Raw
$settings = @{}
foreach ($entry in $web.configuration.appSettings.add) { $settings[$entry.key] = $entry.value }
foreach ($entry in $local.appSettings.add) { $settings[$entry.key] = $entry.value }
$env:INVENTORY_SMOKE_USERNAME = $settings.SeedUserName
$env:INVENTORY_SMOKE_PASSWORD = $settings.SeedPassword
$env:INVENTORY_SMOKE_SIGNING_KEY = $settings.JwtSigningKey
$env:INVENTORY_SMOKE_ISSUER = $settings.JwtIssuer
$env:INVENTORY_SMOKE_AUDIENCE = $settings.JwtAudience
$env:INVENTORY_SMOKE_APP_ROOT = (Resolve-Path .\eShopInventoryApi).Path
$env:INVENTORY_SMOKE_CONNECTION_STRING = ($web.configuration.connectionStrings.add |
    Where-Object name -eq InventoryContext).connectionString
# Set INVENTORY_SMOKE_HOST_PID to the PID of the host you started.
pwsh -NoProfile -File .\tests\smoke\Invoke-InventorySmoke.ps1 -BaseUrl http://localhost:61462
```

Run twice to verify repeatability. Each request has a 30-second timeout
(override with `-TimeoutSec`, from 1 to 120 seconds); redirects are rejected.
The suite stops at the first failed assertion and exits nonzero, including
on cleanup failure. Output contains sanitized per-scenario results, not
credentials, signing material, tokens, or response bodies. Each run uses
unique `SMOKE-<guid>` SKUs and removes only its own records in `finally`,
including after failure; it never resets, drops, or reseeds a database.
If cleanup cannot reach the host, its failure output identifies the run's SKU
prefix for targeted recovery. Keep the host running until cleanup finishes.
The suite checks the final list against its pre-run snapshot; avoid concurrent
writers to this isolated fixture.

Assertions retain the observed .NET Framework response envelopes, including
empty failed-login/404/204 responses and `request.*` ModelState keys. Expanded
validation and JWT expectations initially derive from the controllers, DTO
annotations, and startup configuration; execution evidence and remaining
limitations are recorded in `docs\dotnet-x-to-y\plan.md`. This suite does not
prove exhaustive correctness, startup safety, or database compatibility on
another runtime.

## License

[MIT](LICENSE).
