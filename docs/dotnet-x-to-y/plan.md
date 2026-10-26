## Step 0: Baseline -> reproducible baseline

**Status:** pending

**Source:** The existing ASP.NET Web API 2 application on .NET Framework 4.8.

**Target:** A documented, best-effort baseline with commands, observed results,
and explicit validation gaps. Baseline success is not a prerequisite for Step 1.

### Goal and constraints

The user selected an in-place migration to ASP.NET Core on .NET 10.
Keep `eShopInventory.sln` and `eShopInventoryApi\eShopInventoryApi.csproj` as
the application solution and project. Preserve the six endpoints, stored
stock, Identity users and password hashes, JWT contracts, input validation,
and startup safety gates. Retain Windows and SQL Server LocalDB.

Do not introduce a parallel application, routing cutover, EF Core migration,
ASP.NET Core Identity migration, Swagger, frontend, cloud deployment,
registration, refresh tokens, unrelated hardening, or architectural refactoring.
Test projects are validation infrastructure, not replacement applications.

### Inspected evidence and known prerequisites

- The solution contains one classic web project targeting `v4.8`, with
  `packages.config`, assembly hint paths, and WebApplication MSBuild imports.
  There are no automated tests, CI definitions, SDK selection files, stubs,
  fake authentication, or frontend files in the inspected repository.
- `Startup.cs` initializes EF6 before serving requests and configures OWIN JWT
  validation. `App_Start\WebApiConfig.cs` requires bearer authentication globally,
  exempts login through `[AllowAnonymous]`, and removes XML formatting.
- `DAL\InventoryContext.cs` extends the legacy Identity EF6 context.
  `DatabaseInitializer.cs` creates and seeds only a nonexistent database.
  Existing databases are not migrated, dropped, or reseeded.
- `AuthenticationSettings.cs` requires issuer, audience, seed username,
  seed password, and a Base64 signing key containing at least 32 bytes.
  Password validation requires six characters; usernames are limited to 256.
  Settings validation precedes database initialization even for an existing DB.
- DTOs require a username/password for login and a nonempty SKU of at most
  64 characters plus a nonnegative integer quantity for stock writes.
- `Web.config` configures `System.Data.SqlClient`, LocalDB, EF6, issuer/audience,
  and an ignored external `AppSettings.local.config`. That local file and the
  restored `packages` directory are currently absent. No secrets were read.
- Tool discovery found .NET SDK `10.0.403`, Visual Studio 18 MSBuild and its
  WebApplication targets, the .NET Framework 4.8 reference-assembly directory,
  IIS Express, NuGet CLI, PowerShell 7, SQLCMD, and LocalDB 17 tooling.
  Presence is not proof that builds, the host, or a database work.
- EF6 6.4.4 has a modern .NET-compatible asset path. However, the NuGet framework
  listings for the exact legacy Identity Core and EntityFramework packages,
  version 2.2.3, list only .NET Framework assets. Retaining Identity on .NET 10
  is a compatibility risk to verify, not an established runtime failure or a
  claim of supported targeting. An OWIN adapter cannot change package assets.
- No restore, build, test, server startup, SQL connection, or transformation
  has been run during planning. All execution outcomes remain pending.

### Exact files and scope

Read the existing solution/project, `Web.config`, startup, controllers, DAL,
models, Identity configuration, authentication settings, and `Inventory.http`.
Record sanitized baseline evidence and later step results in this document.
During execution only, supply ignored local credentials and use a uniquely named
validation database. Never use or modify an existing personal database without
approval. If a temporary connection-string change is needed for the legacy host,
record it and restore the prior value afterward without reverting other edits.

### Proposed baseline commands

Run from the repository root, only during approved execution:

```powershell
nuget restore .\eShopInventory.sln -NonInteractive
msbuild .\eShopInventory.sln /t:Build /p:Configuration=Debug /p:VisualStudioVersion=18.0 /verbosity:minimal /nologo
SqlLocalDB info
& 'C:\Program Files\IIS Express\iisexpress.exe' /path:"<absolute-worktree>\eShopInventoryApi" /port:61462
```

Supply a generated local signing key and valid local seed credentials without
printing them. Confirm the connection string selects the isolated validation
database before starting the host. If MSBuild fails to resolve web targets,
retry once with the discovered Visual Studio 18 `WebApplications` parent as
`VSToolsPath`; do not install tooling speculatively.

### Acceptance criteria

Attempt applicable restore, build, local host, and representative API checks.
Record each command and its result, including failed or unavailable checks and
their consequences for later comparisons. If credentials, database isolation,
or hosting cannot be established safely, record the affected checks as
inconclusive and proceed to Step 1. Do not report an unavailable baseline as a
pass. Final build, tests, and live smoke testing remain mandatory.

### Test plan and live test

Use `Inventory.http` and controller source to characterize anonymous stock
access (`401`), failed login (`401`), successful login (`200` with
`access_token`, `token_type: Bearer`, `expires_in: 1800`), and an authenticated
stock list (`200`, ordered by `Id`, with `Id`, `Sku`, `Quantity` properties).
Where runnable, capture sanitized statuses, JSON shapes, error envelopes,
content types, and the initial three seeded SKUs/quantities. Do not save tokens,
credentials, or the signing key. Stop only the IIS Express process started for
these checks.

**Completion rule:** Attempt and document the applicable checks, failures,
unavailable checks, and implications. A missing baseline alone does not block
Step 1. Observed results: pending.

## Step 1: Manual API examples -> repeatable API characterization checks

**Status:** pending

**Source:** `Inventory.http` contains manual requests without assertions.

**Target:** A saved, host-independent API smoke suite usable against both the
legacy host and the migrated application, separate from fast inner-loop tests.

**Dependency:** Step 0's attempt and evidence recording, not a successful baseline.

### Exact files and scope

Add `tests\smoke\Invoke-InventorySmoke.ps1` and document its invocation and
credential inputs in `README.md`. Preserve the manual HTTP examples.
Use PowerShell 7 HTTP/JSON facilities already available here; the repository
has no preferred automated test framework to reuse. Do not add Pester,
Playwright, screenshots, Swagger, or CI solely for API smoke testing.

Accept a base URL and consume credentials/signing material through local
environment variables or secure input, not committed literals or command-line
passwords. Verify the intended host and isolated database before writes.
Use unique SKUs and clean up only records created by the suite, including after
an assertion fails. Timeouts must be bounded, and failures must exit nonzero.
Never delete or reset a database as smoke-test cleanup.

### Acceptance criteria

The saved suite asserts status, JSON property names/types, relevant headers,
and persisted effects for the six endpoints. It covers:

- Login success, wrong credentials, null/missing/invalid login input.
- All stock verbs denied without a token; representative malformed, expired,
  wrongly signed, wrong-issuer, and wrong-audience tokens denied with `401`.
  Expiry tests use expiration beyond the existing 30-second clock skew.
- Authenticated list/read, create (`201` with a usable `Location`), update
  (`204` with no response body), and delete (`204` with no response body).
  Read back creates and updates; read after deletion must return `404`.
- Missing IDs return `404`; null bodies, missing fields, overlong SKUs,
  negative quantities, and malformed JSON return `400`. JSON remains the
  supported response format, including when XML is requested.

Derive error-envelope assertions from observed baseline responses when
available, otherwise label source-derived expectations explicitly. Preserve
PascalCase stock properties and the login response's snake_case properties;
do not silently accept both casing conventions. No authentication bypass.

### Test plan and live test

Proposed invocation, with secrets supplied separately:

```powershell
pwsh -NoProfile -File .\tests\smoke\Invoke-InventorySmoke.ps1 -BaseUrl http://localhost:61462
```

Run against the baseline if it can be hosted, then rerun to establish that
setup and cleanup are repeatable. Record per-scenario expected/actual outcomes.
If baseline hosting remains unavailable, validate the script's syntax and
record live execution as inconclusive; do not make baseline availability a
prerequisite for Step 2. The unchanged legacy application must still build and
run where its prerequisites are available. Observed results: pending.

## Step 2: .NET Framework 4.8 Web API 2 -> ASP.NET Core on .NET 10

**Status:** pending

**Source:** The current web project, System.Web Web API host, OWIN bearer
middleware, EF6, and legacy ASP.NET Identity.

**Target:** The same application project targeting `net10.0`, running on
ASP.NET Core/Kestrel with preserved API and database behavior.

**Dependency:** Step 1's saved characterization checks and explicit baseline gaps.

### Exact files and scope

Perform this as one coherent, atomic transformation. Do not leave a
retarget-only or half-ported project as an intermediate completed step.

- Convert `eShopInventoryApi\eShopInventoryApi.csproj` in place to
  `Microsoft.NET.Sdk.Web`, `net10.0`, and fixed-version `PackageReference`s.
  Preserve assembly identity through `GenerateAssemblyInfo=false`; leave
  nullable and implicit usings disabled to avoid unrelated churn.
  Keep `eShopInventory.sln`, adding only the validation test project below.
- Replace `Startup.cs`'s System.Web/OWIN hosting with an ASP.NET Core entry
  point in `Program.cs`. Adapt or remove `App_Start\WebApiConfig.cs` only as
  its routing, JSON formatting, and global authorization move to Core.
  Adapt the existing two controllers to Core controller/result APIs.
  Do not introduce minimal-API endpoint rewrites.
- Configure native ASP.NET Core JWT bearer authentication with the same
  issuer, audience, signing key, HS256 restriction, signature/expiration
  requirements, and 30-second clock skew. Apply global authenticated access
  and keep login anonymous. Preserve the 30-minute token and claim meanings.
  This app uses no cookie or OWIN user-manager integration, so an OWIN bridge
  is not the default. Use a narrow adapter only if an observed dependency
  requires it and the same behavior can be demonstrated.
- Keep `App_Start\IdentityConfig.cs`, `Models\ApiUser.cs`,
  `DAL\InventoryContext.cs`, and `DAL\DatabaseInitializer.cs` behavior intact.
  Retain EF6, its SQL Server provider, Identity user-store mappings, password
  hasher, username rules, and six-character password policy. Make only
  demonstrated compatibility adjustments. No database schema conversion,
  password reset, unconditional reseed, or `EnsureDeleted` equivalent.
- Replace Web.config's runtime configuration role with
  `eShopInventoryApi\App.config`, retaining its EF6 section, connection string,
  issuer/audience, and external `AppSettings.local.config` reference.
  Use `System.Configuration.ConfigurationManager` and explicit executable
  configuration/output-copy settings. Exclude obsolete System.Web/IIS
  sections from executable configuration. Verify the effective configuration
  path under `dotnet run` and compiled-output execution rather than assuming
  ASP.NET Core consumes Web.config. Do not copy secrets into committed config.
  Remove the obsolete source `Web.config` once nothing uses it.
- Preserve `Security\AuthenticationSettings.cs`'s fail-fast validation and
  validate before database initialization or accepting traffic. Copy ignored
  local settings to the runtime location only when present; missing required
  settings must still stop startup. Preserve `AppSettings.local.config.example`
  and the existing `.gitignore` protection.
- Keep the DTO shapes and EF6 concurrency-to-`404` behavior. Explicitly preserve
  JSON casing/formatting and validation/error envelopes. Avoid Core's default
  camelCase, automatic ProblemDetails, implicit required rules, or `204` for
  null payloads changing the existing contract. Use MVC Newtonsoft integration
  where that is the smallest way to retain the existing serializer.
- Remove `packages.config`, obsolete hint paths, binding redirects, and
  WebApi/SystemWeb hosting packages once their replacements work. Retain only
  direct packages actually needed. Select and record exact compatible package
  versions during execution; no floating-version templates from the map.
- Add `tests\eShopInventoryApi.Tests\eShopInventoryApi.Tests.csproj` and
  focused settings, validation, serialization, and JWT tests using MSTest and
  `dotnet test`. Reuse the app's configuration validator and password hasher
  rather than implementing alternate logic. Keep these fast tests independent
  of LocalDB and the separately run smoke suite.
- Update `README.md` run/setup instructions and `Inventory.http` only where the
  host/configuration changes require it. Keep `http://localhost:61462` as the
  explicit local URL. No HTTPS redirect requirement or production deployment
  claim is added. Update `GLOSSARY.md` only if a referenced term changes.

### Compatibility gate and escalation

First inspect restored package assets and build/runtime diagnostics for the
retained EF6/Identity path. Prove password hashing/verification, user lookup,
database initialization, and actual login on .NET 10. `NU1701` fallback or an
adapter registration is not proof of compatibility; do not hide warnings
globally or call an untested package supported.

Use the smallest demonstrated package or adapter fixes within scope. If
retaining Identity requires an unmaintained fork, custom user-store/password
implementation, schema migration, or other higher-risk change, stop and ask
before choosing it or switching to ASP.NET Core Identity. Record exact
failures and options; do not silently expand this migration.

### Acceptance criteria

The existing application builds and starts on .NET 10, with no unresolved
compatibility failures. All fast tests and the repeatable API suite pass.
Verify that a preexisting validation database and legacy password hash remain
usable without schema changes, and that changing seed settings does not change
an existing user or reseed data. If Step 0 could not produce such a database,
use an approved isolated legacy fixture generated with the original EF6/Identity
model; record fixture provenance. Do not use private database contents.

A fresh isolated database gets the same three records:
`MUG-001: 25`, `TEE-001: 40`, `BAG-001: 15`, and an Identity-created seed user.
A restart preserves stock mutations and user identity. Missing/invalid signing
material and invalid seed settings fail startup before creating a fresh DB.
An already issued legacy JWT remains valid until its original expiry when
issuer, audience, and signing material are unchanged, if a baseline token is
available; otherwise explicitly record that cross-host token check as unverified.

### Test plan and live test

Proposed commands after the atomic edit:

```powershell
dotnet restore .\eShopInventory.sln
dotnet build .\eShopInventory.sln --no-restore -c Debug
dotnet test .\tests\eShopInventoryApi.Tests\eShopInventoryApi.Tests.csproj -c Debug
dotnet run --project .\eShopInventoryApi\eShopInventoryApi.csproj --no-build --urls http://localhost:61462
pwsh -NoProfile -File .\tests\smoke\Invoke-InventorySmoke.ps1 -BaseUrl http://localhost:61462
```

Verify readiness with bounded HTTP checks, not just a running process.
Run against isolated fresh and existing-database cases. Demonstrate failure
paths in separate process launches without weakening gates or altering real
credentials. Record build/test output, sanitized HTTP assertions, database
preservation observations, and any package compatibility limitations.
Stop only processes started for validation. Observed results: pending.

## Step 3: Migrated API -> Run AI-assisted smoke testing

**Status:** pending

**Source:** The in-place .NET 10 candidate with fast tests and saved smoke checks.

**Target:** A reviewable approval gate demonstrating the selected migration
goal and required behavior preservation against the real running application.

**Dependency:** Step 2's completed, buildable, tested, runnable transformation.

### Exact files and scope

Run `tests\smoke\Invoke-InventorySmoke.ps1` against the intended compiled
`eShopInventoryApi` process and isolated LocalDB database. Refine that same
suite only for evidenced assertion/setup defects, not to accept regressions.
Record sanitized final evidence and step outcomes in this plan.
No additional feature work, test platform, or CI rollout.

### Acceptance criteria

- Build the solution in Release and run the focused tests. Launch the compiled
  Release DLL directly to prove executable configuration works without IDE
  assistance and that the responding process is the migrated app.
- Execute the complete smoke suite twice. All required scenarios from Step 1
  must pass both times, with read-after-write persistence, usable `Location`,
  cleanup, authentication rejection, validation, and response casing intact.
- Restart the application and demonstrate persisted inventory and login using
  the same stored Identity user. Verify fresh-database seeding separately
  from preservation of an existing database.
- Document host revision, SDK, package versions, database fixture provenance,
  commands, expected/actual results, and limitations without secrets or tokens.
  Failure takes precedence over inconclusive; no overall pass if a required
  final check is inconclusive. Missing baseline evidence is a comparison
  limitation, not permission to skip final checks.

### Test plan and live test

```powershell
dotnet build .\eShopInventory.sln -c Release
dotnet test .\tests\eShopInventoryApi.Tests\eShopInventoryApi.Tests.csproj -c Release --no-build
dotnet .\eShopInventoryApi\bin\Release\net10.0\eShopInventoryApi.dll --urls http://localhost:61462
pwsh -NoProfile -File .\tests\smoke\Invoke-InventorySmoke.ps1 -BaseUrl http://localhost:61462
pwsh -NoProfile -File .\tests\smoke\Invoke-InventorySmoke.ps1 -BaseUrl http://localhost:61462
```

Run the host in its own tracked process and wait for bounded readiness before
the suite. Restart for persistence checks, then stop only that process.
The observable result is a per-scenario assertion report with two successful
runs, plus fresh/existing-database and startup-gate evidence. Results: pending.

### Map guidance used

Located through the supplied map's root `INDEX.md`:

- `core-dotnet\netfx-to-net10-knowledge.md`: behavior preservation, minimal
  compatibility changes, SDK conversion, EF6 retention, configuration, and
  authentication compatibility. Its parallel-app examples are deliberately
  omitted in favor of the user's in-place requirement.
- `core-dotnet\netfx-deps-to-compat-nugets-knowledge.md`: explicit package
  references and verification of package assets, restore, and runtime behavior.
  Private feeds, central package management, and governance additions are not
  needed for this one-app migration.
- `testing\no-api-smoke-testing-to-ai-assisted-api-smoke-testing-knowledge.md`:
  repeatable API assertions, controlled data, real-host checks, reruns, and
  pass/fail/inconclusive evidence without introducing Swagger.
- The Web API 2 to ASP.NET Core catalog entry is proposed, not an authored
  recipe. The atomic controller/host port and its acceptance criteria above
  are derived from this repository's source. Map prevalence did not determine
  priority or destination.

The retained Identity risk is supported by the package framework listings:
https://www.nuget.org/packages/Microsoft.AspNet.Identity.Core/2.2.3 and
https://www.nuget.org/packages/Microsoft.AspNet.Identity.EntityFramework/2.2.3.

**Review gate:** This document is a proposed plan. No transformation step has
been executed. Review and approve the plan before execution.
