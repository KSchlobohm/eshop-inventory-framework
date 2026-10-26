## Step 0: Baseline -> reproducible baseline

**Status:** done

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
Step 1. This marks completion of the baseline attempt, not comprehensive
application verification.

### Step 0 execution evidence (2026-10-26)

**Outcome:** Restore, build, isolated LocalDB initialization, IIS Express
hosting, and the representative HTTP checks below succeeded against the
unchanged .NET Framework application at
`f7eeb0f09f27208eb80fe296a2f8514851dd4d92`. No transformation or saved smoke
suite was implemented. Steps 1 onward remain pending.

**Environment:** Windows, PowerShell `7.6.9`, Visual Studio 18 MSBuild
`18.9.0-preview-26325-02`, .NET Framework registry release `534296`, SQL Server
LocalDB `17.0.4025.3`, 64-bit IIS Express. Installed .NET SDKs were
`10.0.400-preview.0.26312.103` and `10.0.403`; the baseline used full-framework
MSBuild, not `dotnet build`.

**Isolation and fixture provenance:** Worktree
`C:\dev\work\copilot-worktrees\eshop-inventory-framework\kschlobohm-turbo-telegram`,
branch `kschlobohm-step-0-baseline`. Created a dedicated, previously nonexistent
LocalDB instance `eShopBaseline_1fab2809`, with database
`eShopBaseline_1fab2809_20261026`. The pre-start `DB_ID` query returned `NULL`;
after app startup it returned `5`. Only this new database was queried or
modified. No connection was made to the personal `MSSQLLocalDB` instance.
Temporarily changed only the `InventoryContext` connection string to:

```text
Data Source=(LocalDB)\eShopBaseline_1fab2809;Initial Catalog=eShopBaseline_1fab2809_20261026;Integrated Security=True;MultipleActiveResultSets=True
```

Generated settings from the existing example into ignored
`eShopInventoryApi\AppSettings.local.config`: a fixture-only username,
a random 24-byte Base64 password, and a separate random 32-byte Base64 signing
key, using `RandomNumberGenerator.GetBytes`. No secret values or tokens are
included here or committed. `git check-ignore` confirmed the file is ignored.
The settings file and isolated database are retained locally for later
compatibility checks, not copied into repository evidence. Database files are
`%USERPROFILE%\eShopBaseline_1fab2809_20261026.mdf` and
`%USERPROFILE%\eShopBaseline_1fab2809_20261026_log.ldf`.
The fixture contains three seeded stocks and one Identity user with a non-null
password hash. No hash or user ID was printed. Temporary CRUD records with
SKU `BASELINE-1fab2809`, IDs `4` and `5`, were deleted and absence verified;
do not assume the next generated ID is `4`.

**Commands and outcomes:** Run from the worktree root; all commands below
completed successfully. The restore emitted no errors, the original build
emitted the application DLL with no warning/error output, and no web-target
fallback or tooling installation was necessary.

```powershell
nuget restore .\eShopInventory.sln -NonInteractive -Verbosity quiet
msbuild .\eShopInventory.sln /t:Build /p:Configuration=Debug /p:VisualStudioVersion=18.0 /verbosity:minimal /nologo
SqlLocalDB info
SqlLocalDB create eShopBaseline_1fab2809 -s
sqlcmd -S '(LocalDB)\eShopBaseline_1fab2809' -d master -E -l 15 -b -Q "SET NOCOUNT ON; SELECT DB_ID(N'eShopBaseline_1fab2809_20261026') AS ValidationDatabaseId;"
git check-ignore eShopInventoryApi\AppSettings.local.config
# After generating ignored settings and checking the isolated connection string:
& 'C:\Program Files\IIS Express\iisexpress.exe' "/path:$PWD\eShopInventoryApi" /port:61462
```

Port `61462` had no listener before launch. IIS Express reported successful
registration of `http://localhost:61462/` for this worktree and logged each
request/status below. The first anonymous request used a 60-second timeout;
subsequent requests used 30 seconds. `Accept: application/json` was used except
for the explicitly XML-requested list. POST/PUT bodies used
`Content-Type: application/json`. Authentication used the real seed user,
Identity password verification, and a real issued JWT held only in memory.

The one-time HTTP command pattern was
`Invoke-WebRequest -Uri "http://localhost:61462$path" -Method $method
-TimeoutSec 30 -SkipHttpErrorCheck -Headers $headers`, adding
`-ContentType application/json -Body $body` for body-bearing requests.
The table records the exact paths and nonsecret bodies. Login credentials
were read from the generated XML rather than supplied as command-line literals:

```powershell
[xml]$local = Get-Content eShopInventoryApi\AppSettings.local.config
$settings = @{}
foreach ($entry in $local.appSettings.add) { $settings[$entry.key] = $entry.value }
$body = @{ UserName = $settings.SeedUserName; Password = $settings.SeedPassword } | ConvertTo-Json -Compress
$login = Invoke-WebRequest http://localhost:61462/api/auth/login -Method Post -ContentType application/json -Body $body -Headers @{ Accept = 'application/json' } -SkipHttpErrorCheck -TimeoutSec 30
$auth = @{ Accept = 'application/json'; Authorization = ('Bearer ' + ($login.Content | ConvertFrom-Json).access_token) }
```

**Observed live results:** JSON responses below had
`Content-Type: application/json; charset=utf-8`. "Empty" means zero response
body and no `Content-Type` header, not a JSON `[]` payload. Stock property
names were PascalCase; login property names were snake_case.

| Request / input | Expected | Observed response |
| --- | --- | --- |
| Anonymous `GET /api/stock` | `401` | `401`, JSON `{"Message":"Authorization has been denied for this request."}` |
| `POST /api/auth/login`, fixture username with `Password: incorrect-baseline-password` | `401` | `401`, empty; separately confirmed raw/content lengths `0` |
| `POST /api/auth/login`, `null` | `400` | `400`, JSON `{"Message":"A login request is required."}` |
| `POST /api/auth/login`, `{}` | `400` | `400`, JSON validation envelope shown below |
| `POST /api/auth/login`, generated valid credentials | `200` | `200`, JSON `{"access_token":"<redacted string>","token_type":"Bearer","expires_in":1800}` |
| Authenticated `GET /api/stock` | `200`, ordered seed list | `200`, exact three-record JSON shown below |
| Authenticated `GET /api/stock/1` | `200` | `200`, JSON `{"Id":1,"Sku":"MUG-001","Quantity":25}` |
| Authenticated `GET /api/stock/2147483647` | `404` | `404`, empty |
| Authenticated `POST /api/stock`, `null` | `400` | `400`, JSON `{"Message":"A stock record is required."}` |
| Authenticated `POST /api/stock`, `{"Sku":"","Quantity":-1}` | `400` | `400`, JSON validation envelope shown below |
| Authenticated `POST /api/stock`, malformed body `{` | `400` | `400`, JSON `{"Message":"A stock record is required."}` (not a parser-detail envelope) |
| Authenticated `GET /api/stock`, `Accept: application/xml` | JSON remains supported | `200`, same JSON seed list and JSON content type |
| Authenticated `POST /api/stock`, `{"Sku":"BASELINE-1fab2809","Quantity":7}` | `201`, usable `Location` | `201`, JSON `{"Id":5,"Sku":"BASELINE-1fab2809","Quantity":7}`; `Location: http://localhost:61462/api/stock/5` |
| Authenticated `GET /api/stock/5` | Persisted create | `200`, same created JSON |
| Authenticated `PUT /api/stock/5`, `{"Sku":"BASELINE-1fab2809","Quantity":9}` | `204` | `204`, empty |
| Authenticated `GET /api/stock/5` | Persisted update | `200`, JSON `{"Id":5,"Sku":"BASELINE-1fab2809","Quantity":9}` |
| Authenticated `DELETE /api/stock/5` | `204` | `204`, empty |
| Authenticated `GET /api/stock/5` | Persisted deletion | `404`, empty |
| Authenticated final `GET /api/stock` | Seeds preserved, fixture removed | `200`, exact original seed list |

Observed seed list, both before and after CRUD:

```json
[{"Id":1,"Sku":"MUG-001","Quantity":25},{"Id":2,"Sku":"TEE-001","Quantity":40},{"Id":3,"Sku":"BAG-001","Quantity":15}]
```

Observed missing-login-fields envelope:

```json
{"Message":"The request is invalid.","ModelState":{"request.UserName":["The UserName field is required."],"request.Password":["The Password field is required."]}}
```

Observed invalid-stock envelope:

```json
{"Message":"The request is invalid.","ModelState":{"request.Sku":["The Sku field is required."],"request.Quantity":["The field Quantity must be between 0 and 2147483647."]}}
```

**Evidence correction, cleanup, and final state:** The first one-time run
captured formatted HTTP evidence into PowerShell assignments instead of
printing it. IIS logs still recorded its requests, including fixture ID `4`;
its cleanup/read-after-delete completed. Repeated the same representative
requests with the evidence printer corrected, yielding the visible responses
above and fixture ID `5`. This was an evidence-capture correction, not a retry
of failing application behavior. SQL independently confirmed exactly three
seed rows and one user with a stored hash:

```powershell
sqlcmd -S '(LocalDB)\eShopBaseline_1fab2809' -d eShopBaseline_1fab2809_20261026 -E -l 15 -b -Q "SET NOCOUNT ON; SELECT Id,Sku,Quantity FROM dbo.StockRecords ORDER BY Id; SELECT COUNT(*) AS SeedUserCount, SUM(CASE WHEN PasswordHash IS NOT NULL THEN 1 ELSE 0 END) AS UsersWithStoredPasswordHash FROM dbo.AspNetUsers;"
```

Stopped only the IIS Express validation process tree started by this session
(tool shell ID `step0-iis`); confirmed no listener remained on `61462`.
Ran `SqlLocalDB stop eShopBaseline_1fab2809`, retaining the fixture files.
Restored only the temporary connection-string edit; then
`git diff --exit-code -- eShopInventoryApi\Web.config` returned `0`.
Rebuilt the restored, unchanged solution successfully using:

```powershell
& 'C:\Program Files\Microsoft Visual Studio\18\IntPreview\MSBuild\Current\Bin\MSBuild.exe' .\eShopInventory.sln /t:Build /p:Configuration=Debug /p:VisualStudioVersion=18.0 /verbosity:minimal /nologo
```

Only this plan has tracked changes. The ignored settings and build/package
outputs remain local. **Do not launch with the restored default connection
string for future validation:** explicitly select the isolated fixture first,
start its named instance, and supply the retained settings without printing
secrets. Do not delete or reset the retained fixture.

**Failures, unavailable checks, and implications:** No restore/build/hosting
blocker or unexpected response was observed in these representative checks.
They do not establish exhaustive behavior preservation. No saved suite,
all-verbs anonymous authorization matrix, invalid/expired/wrong-signature/
wrong-issuer/wrong-audience JWT matrix, validation boundary matrix, startup
failure matrix, or schema/password-hash compatibility under .NET 10 was run.
Those remain unverified and belong to Steps 1 onward. In particular, carry
forward the empty failed-login/`404`/`204` responses, exact `ModelState` keys,
malformed-JSON null-request envelope, PascalCase stock JSON, and XML-request
JSON fallback when implementing characterization checks. Retaining this
freshly generated EF6/Identity fixture enables later existing-data checks but
does not prove compatibility with personal or historical databases.

## Step 1: Manual API examples -> repeatable API characterization checks

**Status:** done

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
run where its prerequisites are available.

### Step 1 execution evidence (2026-10-26)

**Outcome:** Added the distinct PowerShell 7 approval-gate suite at
`tests\smoke\Invoke-InventorySmoke.ps1` and documented prerequisites, inputs,
and invocation in `README.md`. Two complete executions against the real,
unchanged .NET Framework application passed all **37 scenario groups each**,
with exit code `0` and verified cleanup. No application source, project,
solution, packages, or `Inventory.http` changes were made. No migration,
new test platform, CI, push, or PR belongs to this step.

**Revision and environment:** Verified
`git merge-base --is-ancestor 6eafe55e72d1aaf129d582fc8000475446e6bb6a HEAD`
returned `0` before work. Validation used that predecessor application,
Windows, PowerShell 7.6.9, Visual Studio 18 MSBuild, SQLCMD, LocalDB, and
64-bit IIS Express. Worktree:
`C:\dev\work\copilot-worktrees\eshop-inventory-framework\kschlobohm-refactored-train`;
branch `kschlobohm-step-1-api-characterization`.

**Isolation and secrets:** Reused only the explicitly approved Step 0 fixture:
instance `eShopBaseline_1fab2809`, database
`eShopBaseline_1fab2809_20261026`. Securely copied only the approved ignored
`AppSettings.local.config` into this worktree; no values were printed or
committed. Loaded credentials, signing material, issuer, and audience into
the suite's documented process environment. Temporarily selected exactly
Step 0's isolated connection string in this worktree's `Web.config`, checked
it before host startup, and restored that edit after stopping the host.
The personal `MSSQLLocalDB` database was never contacted.

Confirmed port `61462` was unused before starting this worktree's IIS Express
process, PID `20764`. Readiness returned anonymous `401`. The suite checked
the absolute application directory in the host command line, the registered
HTTP.sys URL/request queue and its attached PID, the configured connection
string, and SQL `DB_NAME()` before its HTTP requests. It permits only loopback
URLs and dedicated named `eShopBaseline_`/`eShopSmoke_` LocalDB fixtures.
Each request uses a bounded 30-second timeout and disallows redirects.

**Exact command sequence:** Run from this worktree root. Environment inputs
were populated as documented in the README; no password or signing material
was supplied on a command line.

```powershell
git merge-base --is-ancestor 6eafe55e72d1aaf129d582fc8000475446e6bb6a HEAD
nuget restore .\eShopInventory.sln -NonInteractive -Verbosity quiet
msbuild .\eShopInventory.sln /t:Build /p:Configuration=Debug /p:VisualStudioVersion=18.0 /verbosity:minimal /nologo
git check-ignore eShopInventoryApi\AppSettings.local.config
SqlLocalDB start eShopBaseline_1fab2809
sqlcmd -S '(LocalDB)\eShopBaseline_1fab2809' -d eShopBaseline_1fab2809_20261026 -E -l 15 -t 15 -b -Q 'SET NOCOUNT ON; SELECT Id,Sku,Quantity FROM dbo.StockRecords ORDER BY Id; SELECT COUNT(*) AS UserCount FROM dbo.AspNetUsers;'
# After selecting and guarding the isolated connection:
& 'C:\Program Files\IIS Express\iisexpress.exe' "/path:$PWD\eShopInventoryApi" /port:61462
# In the configured validation shell, once the host is ready:
pwsh -NoProfile -File .\tests\smoke\Invoke-InventorySmoke.ps1 -BaseUrl http://localhost:61462
pwsh -NoProfile -File .\tests\smoke\Invoke-InventorySmoke.ps1 -BaseUrl http://localhost:61462
sqlcmd -S '(LocalDB)\eShopBaseline_1fab2809' -d eShopBaseline_1fab2809_20261026 -E -l 15 -t 15 -b -Q "SET NOCOUNT ON; SELECT Id,Sku,Quantity FROM dbo.StockRecords ORDER BY Id; SELECT COUNT(*) AS SuiteRowsRemaining FROM dbo.StockRecords WHERE Sku LIKE 'SMOKE-%'; SELECT COUNT(*) AS UserCount, SUM(CASE WHEN PasswordHash IS NOT NULL THEN 1 ELSE 0 END) AS UsersWithStoredPasswordHash FROM dbo.AspNetUsers; SELECT IDENT_CURRENT('dbo.StockRecords') AS LastStockIdentity;"
```

Restore/build succeeded with no warning/error output and emitted the original
application DLL. Two initial script-development invocations exited `1`
before completing preflight and before any HTTP scenario or write: a
PowerShell dictionary adapter interpreted the connection builder's property
assignment as a dictionary key, and the original error reporter itself assumed
an unavailable `Name` property. Corrected the harness to use
`set_ConnectionString` and safe typed/line-number reporting. No application
assertion was relaxed, skipped, retried to manufacture a pass, or reclassified
as inconclusive. Subsequent complete runs used the same corrected script.

**Per-scenario expected/actual results:** Every row below passed in **both**
complete runs. `JSON` includes the exact
`application/json; charset=utf-8` content type. `Empty` includes zero body
length and absence of `Content-Type`. Error objects and stock/login objects
assert exact, case-sensitive property sets, not alternative casing.

| Scenario | Required expectation | Run 1 actual | Run 2 actual |
| --- | --- | --- | --- |
| Host/database preflight | Intended own host and isolated DB before writes | Verified | Verified |
| Valid Identity login | `200` JSON; `access_token` string JWT, `token_type` string `Bearer`, integer `expires_in=1800` | Exact match | Exact match |
| Authorized stock list | `200` JSON array; ascending unique positive integer `Id`, string `Sku`, nonnegative integer `Quantity` | Exact match, three seeds | Exact match, three seeds |
| Missing ID GET/PUT/DELETE | Each `404`, empty; sentinel `-2147483648` | All match | All match |
| Wrong login password | `401`, empty | Exact match | Exact match |
| Null login | `400`, only `Message: A login request is required.` | Exact match | Exact match |
| Malformed login `{` | Same null-request `400` envelope | Exact match | Exact match |
| Missing login fields `{}` | `400`, `Message: The request is invalid.`, exact required-field arrays under `request.UserName` and `request.Password` | Exact match | Exact match |
| Invalid login username, length 257 | `400`, same validation Message; `request.UserName: ["The field UserName must be a string with a maximum length of 256."]` | Exact match | Exact match |
| Anonymous GET list, GET item, POST, PUT, DELETE | All five `401` JSON; only `Message: Authorization has been denied for this request.`; Bearer challenge | All match | All match |
| Locally signed valid JWT control | `200` authorized list; validates supplied key/issuer/audience for negative-token controls | Match | Match |
| Malformed JWT | `401`, same exact anonymous JSON Message and Bearer challenge | Exact match | Exact match |
| Expired JWT | Same `401`; expiration 120 seconds ago, beyond 30-second skew | Exact match | Exact match |
| Wrong-signature JWT | Same `401`; fresh unrelated random key | Exact match | Exact match |
| Wrong-issuer JWT | Same `401`; otherwise valid signature/audience/lifetime | Exact match | Exact match |
| Wrong-audience JWT | Same `401`; otherwise valid signature/issuer/lifetime | Exact match | Exact match |
| Create unique fixture | `201` JSON; exact typed `Id/Sku/Quantity`; same-origin usable `/api/stock/{id}` Location; subsequent GET confirms SKU and quantity 7 | Exact match, ID 6 | Exact match, ID 7 |
| POST and PUT null stock | Each `400`, only `Message: A stock record is required.`; no persisted change | Both match | Both match |
| POST and PUT malformed `{` | Each same null-stock `400` envelope, not parser details; no persisted change | Both match | Both match |
| POST and PUT missing fields `{}` | Each `400`, validation Message; `request.Sku: ["The Sku field is required."]`, `request.Quantity: ["The Quantity field is required."]`; no persisted change | Both match | Both match |
| POST and PUT empty SKU plus negative quantity | Each `400`, exact Step 0 validation envelope; no persisted change | Both match | Both match |
| POST and PUT SKU length 65 | Each `400`, validation Message; `request.Sku: ["The field Sku must be a string with a maximum length of 64."]`; no persisted change | Both match | Both match |
| POST and PUT quantity -1 | Each `400`, validation Message; `request.Quantity: ["The field Quantity must be between 0 and 2147483647."]`; no persisted change | Both match | Both match |
| XML Accept list | `200`, JSON content type and same typed array as JSON Accept | Exact match | Exact match |
| Valid update | `204`, empty; subsequent GET confirms changed unique SKU and quantity 9 | Exact match | Exact match |
| Delete fixture | `204`, empty; subsequent GET `404`, empty | Exact match | Exact match |
| Final list and finally cleanup | Exact pre-run list preserved; no run-owned SKUs remain | Match; cleanup pass | Match; cleanup pass |

Validation Message and ModelState arrays also assert their JSON string/array
types. Every invalid stock write is followed by a read confirming the fixture's
original SKU/quantity and a list count check. The suite generates fresh
`SMOKE-<guid>` fixture SKUs per invocation, does not assume the next identity,
and its `finally` cleanup selects only exact run-owned SKUs. Cleanup failure
also exits nonzero and identifies only the run's nonsecret SKU prefix.

**Expectation provenance:** Step 0 supplied the observed login, anonymous GET,
list/read/create/update/delete, null/missing login, null/invalid/malformed
stock, empty-response, content-type, Location, and XML-fallback contracts.
The expanded anonymous-verbs/JWT matrix, missing PUT/DELETE, malformed and
overlong login, missing quantity, overlong SKU, PUT validation envelopes,
and Bearer challenge initially used source-derived expectations from the
controllers, DTO annotations, and `Startup.cs`. These expectations were not
claimed as prior observations; both executions now provide live evidence
for them. No application behavior contradicted a required assertion.

**Cleanup and handoff:** SQL independently confirmed exactly the original
three rows (`1/MUG-001/25`, `2/TEE-001/40`, `3/BAG-001/15`), zero `SMOKE-%`
rows, one Identity user, and one non-null stored password hash. The two suite
fixtures, IDs `6` and `7`, were deleted; last identity is `7`, not the next ID.
No database was dropped, reset, reseeded, or deleted. Stopped only this
session's own IIS Express process tree (`step1-iis`), verified no listener
remained, and stopped the retained named LocalDB instance. The fixture and
ignored local settings remain available for the next step; explicitly restore
the isolated connection before launching any subsequent validation host.
The default connection string was restored without launching against it.
Final verification commands below also returned `0`; the restored unchanged
application rebuilt successfully, the script parsed without errors, and
`git status --short` showed only the three intended deliverable paths.

```powershell
# After stopping only the owned host process tree:
Get-NetTCPConnection -LocalPort 61462 -State Listen -ErrorAction SilentlyContinue
Get-Process -Id 20764 -ErrorAction SilentlyContinue
# Both queries above returned no results.
SqlLocalDB stop eShopBaseline_1fab2809
git diff --exit-code -- eShopInventoryApi
msbuild .\eShopInventory.sln /t:Build /p:Configuration=Debug /p:VisualStudioVersion=18.0 /verbosity:minimal /nologo
git diff --check
$parseErrors = $null
$tokens = $null
[void][System.Management.Automation.Language.Parser]::ParseFile(
    (Join-Path $PWD 'tests\smoke\Invoke-InventorySmoke.ps1'),
    [ref]$tokens, [ref]$parseErrors)
# $parseErrors.Count was 0.
```

**Remaining unverified behavior:** No .NET 10 migration or runtime/database
compatibility check, startup-failure matrix, historical/personal database,
load/concurrency, or exhaustive validation/security check belongs to this
step. Mid-run assertion/transport failure cleanup is implemented in `finally`
but was not deliberately fault-injected; both actual runs exercised successful
cleanup and verified absence. Database shape/password-hash compatibility and
future fast tests remain later-step work.

## Step 2: .NET Framework 4.8 Web API 2 -> ASP.NET Core on .NET 10

**Status:** done

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
Stop only processes started for validation. Observed results are recorded below.

### Step 2 execution evidence (2026-10-26)

**Outcome:** Converted the existing application project in place to
`Microsoft.NET.Sdk.Web` / `net10.0` and Kestrel. Debug restore/build succeeded,
all **29 fast MSTest cases passed**, the saved real-host suite passed all
**37 scenario groups with cleanup**, and the existing/fresh database,
restart, executable-configuration, and **10 startup-failure gates** passed.
Step 3 remains pending; its Release build and two final smoke runs were not
performed here. No session, replacement app, schema migration, push, or PR
was created.

**Revision and scope:** Verified
`git merge-base --is-ancestor f49e7fc1aaea41692060f34d44bd6cc0901fd7d4 HEAD`
returned `0` before editing. Worktree:
`C:\dev\work\copilot-worktrees\eshop-inventory-framework\kschlobohm-legendary-fiesta`;
branch `kschlobohm-step-2-dotnet-10-migration`. Kept the existing solution and
application project, original `AssemblyInfo.cs`, DTOs, Identity configuration,
EF6 context/initializer, and manual `Inventory.http`. Added only the planned
MSTest project to the solution. Removed obsolete OWIN/Web API registration,
`packages.config`, assembly hint paths, and source `Web.config`.

`Program.cs` explicitly selects the adjacent `eShopInventoryApi.dll.config`
before reading settings or initializing EF6. The SDK builds that executable
configuration from `App.config`, and conditionally copies the ignored local
XML alongside it. Native JwtBearer retains HS256, issuer/audience, required
signature/expiry, and 30-second skew; login retains its original claims and
30-minute expiry. A shared controller helper retains validation envelopes
and `request.*` keys. Newtonsoft/default contract resolution preserves casing;
no `[ApiController]` automatic ProblemDetails responses are introduced.
Global authenticated access exempts only the anonymous login action.
The saved suite changed only its host/configuration preflight to recognize
the actual compiled executable XML; all HTTP assertions remain unchanged.

**Fixed packages and actual restored assets:**

| Package | Version | Selected assets / reason |
| --- | --- | --- |
| EntityFramework | 6.4.4 | Existing version; `netstandard2.1` EF6 and SQL Server provider |
| Microsoft.AspNet.Identity.Core | 2.2.3 | Existing `net45` user manager, validators, password hasher |
| Microsoft.AspNet.Identity.EntityFramework | 2.2.3 | Existing `net45` user store and schema mappings |
| Microsoft.AspNetCore.Authentication.JwtBearer | 10.0.14 | `net10.0` native host authentication |
| Microsoft.AspNetCore.Mvc.NewtonsoftJson | 10.0.14 | `net10.0` serializer integration |
| System.Configuration.ConfigurationManager | 10.0.0 | Executable XML configuration |
| System.Data.SqlClient | 4.8.6 | Explicit EF6 provider dependency; `netcoreapp2.1` reference/runtime plus Windows runtime asset |
| Microsoft.NET.Test.Sdk | 18.10.1 | Test project only |
| MSTest.TestAdapter / MSTest.TestFramework | 4.4.1 | Test project only |

Host packages resolve `System.IdentityModel.Tokens.Jwt` **8.19.2** and
Newtonsoft.Json **13.0.3** transitively. Inspected `obj\project.assets.json`,
not just package framework listings. EF6's unconstrained restore initially
selected SqlClient 4.8.1 and reported NU1902/NU1903 advisories; pinning 4.8.6
removed those diagnostics without changing provider/schema. The only final
build warnings are the two retained Identity packages' **NU1701** warnings
(reported for both projects). They are not suppressed. Runtime tests below
demonstrate this application's exercised paths, **not supported modern
targeting or comprehensive compatibility for those Framework-only packages**.
No OWIN bridge, fork, custom Identity implementation, or password conversion
was needed.

**Commands and environment:** Windows; .NET SDK **10.0.403**, installed .NET /
ASP.NET Core **10.0.14** runtime, PowerShell **7.6.9**, SQLCMD, and the retained
LocalDB **17.0.4025.3** instance. NuGet used the already configured
`azure-default` package-feed proxy. Direct `api.nuget.org` metadata probes
failed TLS negotiation; the configured proxy and actual restore worked.

```powershell
dotnet restore .\eShopInventory.sln --verbosity minimal
dotnet build .\eShopInventory.sln --no-restore -c Debug --verbosity minimal
dotnet test .\tests\eShopInventoryApi.Tests\eShopInventoryApi.Tests.csproj -c Debug --no-build --no-restore --logger 'console;verbosity=normal'
# After securely copying only the approved ignored fixture settings:
dotnet build .\eShopInventory.sln --no-restore -c Debug --verbosity quiet
SqlLocalDB start eShopBaseline_1fab2809
# Temporarily select only the isolated fixture in the compiled XML.
dotnet <absolute-worktree>\eShopInventoryApi\bin\Debug\net10.0\eShopInventoryApi.dll --urls http://localhost:61462
pwsh -NoProfile -File .\tests\smoke\Invoke-InventorySmoke.ps1 -BaseUrl http://localhost:61462
# Stop the direct host before testing the other entry point:
dotnet run --project <absolute-worktree>\eShopInventoryApi\eShopInventoryApi.csproj --no-build --no-restore --urls http://localhost:61462
# Separate stopped-host configuration changes select the fresh/gate databases.
```

The live orchestration script and redirected host diagnostics are session
artifacts, not application or committed test infrastructure. An initial
orchestration invocation stopped during readiness polling because a normal
two-second HTTP timeout was not handled by its polling loop. No application
assertion or saved-suite scenario had run. Cleanup stopped that owned process
and instance. Corrected only the polling loop to handle timeout cancellation
within its bounded retry window; the next invocation completed every gate.
No failed application assertion was relaxed, skipped, or called inconclusive.

**Observed acceptance results:**

| Gate | Expected / actual |
| --- | --- |
| Fast tests, independent of LocalDB | 29/29 passed: legacy Identity V2 hasher and six-character policy, required/invalid settings and username limits, DTO boundaries, JSON/envelopes, JWT policy/claims/skew and rejected expiry/issuer/audience/signature/algorithm/missing expiry |
| Existing direct-DLL host | PID 20964; readiness `401`, actual legacy-user login `200`; output XML used while working directory was repository root |
| Saved suite | 37/37 passed: exact login/error/type/casing/header contracts, all anonymous verbs, negative JWT controls, CRUD and read-back, all validation cases, XML Accept JSON fallback, original-list snapshot and finally cleanup |
| Existing database preservation | SQL SHA-256 fingerprints of table/column definitions and the complete stored Identity user record (including hash) matched before/after; exact original stock snapshot matched; fingerprints and secrets were not printed |
| Existing restart / changed seeds | `dotnet run` listener PID 18716 was its verified child; changed seed username/password did not reseed stock or change existing user/hash; original credentials still logged in |
| Fresh database | `eShopSmoke_Step2_fc95572d56` was absent beforehand, then seeded exactly `1/MUG-001/25`, `2/TEE-001/40`, `3/BAG-001/15` and an Identity-created user; real login `200` |
| Fresh restart / persistence | Created one unique `STEP2-<guid>` stock row with quantity 17; after stop/restart with changed seed settings it and the original user/hash remained unchanged; original login worked; deleted only that created row |
| Missing external XML / missing signing key | Each exited nonzero with settings `ConfigurationErrorsException`; no listener and gate DB still absent |
| Invalid Base64 / short signing key | Each same fail-before-database/listening result |
| Missing issuer / missing audience | Each same fail-before-database/listening result |
| Short / missing seed password | Each same fail-before-database/listening result |
| Missing / 257-character seed username | Each same fail-before-database/listening result |

**Fixture provenance and cleanup:** Securely copied only the explicitly
approved ignored Step 1 `AppSettings.local.config`; no secret was printed,
passed as a command-line password, or committed. Reused the original legacy
fixture on `(LocalDB)\eShopBaseline_1fab2809`, database
`eShopBaseline_1fab2809_20261026`, with original three stocks and original
Identity user/hash created by the Framework host. The smoke run's record
was removed and the exact prior stock list verified; identity sequence
advancement is expected, so do not assume a next stock ID.
No schema or user/password conversion occurred.

The separate fresh database above remains available; its temporary stock row
was deleted. Startup checks used the distinct
`eShopSmoke_Step2_fc95572d56_gates`, which remained nonexistent throughout all
10 checks. No database was dropped, reset, or deleted. No connection was made
to personal `MSSQLLocalDB`. Stopped only owned host PIDs and the named instance
started for this validation; verified port 61462 has no remaining listener
and the named instance is stopped. Restored the output XML and ignored
settings after validation, retaining the source default connection without
ever launching against it. Future validation must explicitly select the
isolated fixture again before starting.

**Limitations and remaining work:** No original unexpired legacy JWT was
retained, so cross-host validation of an already-issued legacy token is
**unverified**, not passed. JWT claim semantics and locally signed controls
were exercised. Identity 2.2.3 remains Framework-only with NU1701; the tested
Windows/EF6 paths work, but no broader platform/support claim is made.
Release validation and two final saved-suite runs belong only to Step 3.

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
