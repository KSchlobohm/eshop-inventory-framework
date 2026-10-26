#Requires -Version 7.0
[CmdletBinding()]
param(
    [Parameter(Mandatory)][uri]$BaseUrl,
    [ValidateRange(1, 120)][int]$TimeoutSec = 30
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$passed = 0
$failed = $false
$currentCheck = 'preflight'
$token = $null
$prefix = 'SMOKE-' + [guid]::NewGuid().ToString('N')
$sku = $prefix + '-create'
$updatedSku = $prefix + '-update'
$overlongSku = $prefix.PadRight(65, 's')
$missingId = -2147483648

function Assert-That([bool]$Condition, [string]$Description) {
    if (-not $Condition) {
        $exception = [InvalidOperationException]::new($Description)
        $exception.Data['SmokeAssertion'] = $true
        throw $exception
    }
}

function Assert-Keys($Object, [string[]]$Keys) {
    Assert-That ($Object -is [System.Collections.IDictionary]) 'Expected a JSON object.'
    $actual = @($Object.Keys | Sort-Object -CaseSensitive)
    $expected = @($Keys | Sort-Object -CaseSensitive)
    Assert-That (($actual -join '|') -ceq ($expected -join '|')) "Expected exact JSON keys: $($expected -join ', ')."
}

function Send-Request([string]$Method, [string]$Path, [string]$Body = '', [string]$Bearer = '', [string]$Accept = 'application/json') {
    $headers = @{ Accept = $Accept }
    if ($Bearer) { $headers.Authorization = 'Bearer ' + $Bearer }
    $arguments = @{
        Uri = [uri]::new($BaseUrl, $Path); Method = $Method; Headers = $headers
        TimeoutSec = $TimeoutSec; SkipHttpErrorCheck = $true; MaximumRedirection = 0
    }
    if ($Body -ne '') { $arguments.Body = $Body; $arguments.ContentType = 'application/json' }
    # Never emit raw responses, request bodies, tokens, or exception details.
    try { Invoke-WebRequest @arguments }
    catch { throw "HTTP transport failed for $Method $Path." }
}

function Expect-Status($Response, [int]$Status) {
    Assert-That ([int]$Response.StatusCode -eq $Status) "Expected HTTP $Status; observed $([int]$Response.StatusCode)."
}

function Read-Json($Response) {
    Assert-That ([string]$Response.Headers['Content-Type'] -ceq 'application/json; charset=utf-8') 'Expected JSON Content-Type with UTF-8 charset.'
    try { ConvertFrom-Json -InputObject $Response.Content -AsHashtable -NoEnumerate }
    catch { throw 'Expected valid JSON response.' }
}

function Expect-Empty($Response, [int]$Status) {
    Expect-Status $Response $Status
    Assert-That ($Response.RawContentLength -eq 0 -and $Response.Content.Length -eq 0) 'Expected an empty response body.'
    Assert-That (-not $Response.Headers.ContainsKey('Content-Type')) 'Expected no Content-Type on an empty response.'
}

function Expect-Error($Response, [string]$Message, [System.Collections.IDictionary]$ModelState = @{}) {
    Expect-Status $Response 400
    $json = Read-Json $Response
    if ($ModelState.Count) {
        Assert-Keys $json @('Message', 'ModelState')
        Assert-Keys $json.ModelState @($ModelState.Keys)
        foreach ($key in $ModelState.Keys) {
            $errors = $json.ModelState[$key]
            Assert-That ($errors -is [array] -and $errors.Count -eq 1 -and $errors[0] -is [string] -and $errors[0] -ceq $ModelState[$key]) "Unexpected ModelState error for $key."
        }
    }
    else { Assert-Keys $json @('Message') }
    Assert-That ($json.Message -is [string] -and $json.Message -ceq $Message) 'Unexpected error Message.'
}

function Expect-Denied($Response) {
    Expect-Status $Response 401
    $json = Read-Json $Response
    Assert-Keys $json @('Message')
    Assert-That ($json.Message -ceq 'Authorization has been denied for this request.') 'Unexpected authorization Message.'
    Assert-That ((@($Response.Headers['WWW-Authenticate']) -join ',') -match '(?i)\bBearer\b') 'Expected a Bearer challenge.'
}

function Assert-Stock($Stock) {
    Assert-Keys $Stock @('Id', 'Sku', 'Quantity')
    Assert-That (($Stock.Id -is [long] -or $Stock.Id -is [int]) -and $Stock.Id -gt 0) 'Expected a positive integer Id.'
    Assert-That ($Stock.Sku -is [string]) 'Expected a string Sku.'
    Assert-That (($Stock.Quantity -is [long] -or $Stock.Quantity -is [int]) -and $Stock.Quantity -ge 0) 'Expected a nonnegative integer Quantity.'
}

function Get-StockList([string]$Accept = 'application/json') {
    $response = Send-Request GET '/api/stock' -Bearer $token -Accept $Accept
    Expect-Status $response 200
    $list = Read-Json $response
    Assert-That ($list -is [array]) 'Expected a JSON array.'
    $previous = 0
    foreach ($stock in $list) {
        Assert-Stock $stock
        Assert-That ($stock.Id -gt $previous) 'Expected stock ordered by unique ascending Id.'
        $previous = $stock.Id
    }
    return ,$list
}

function Expect-Record([int]$Id, [string]$ExpectedSku, [int]$Quantity) {
    $response = Send-Request GET "/api/stock/$Id" -Bearer $token
    Expect-Status $response 200
    $stock = Read-Json $response
    Assert-Stock $stock
    Assert-That ($stock.Id -eq $Id -and $stock.Sku -ceq $ExpectedSku -and $stock.Quantity -eq $Quantity) 'Read-after-write values differ.'
}

function Check([string]$Name, [scriptblock]$Action) {
    $script:currentCheck = $Name
    & $Action
    $script:passed++
    Write-Output "PASS $Name"
}

function Encode-Base64Url([byte[]]$Bytes) {
    [Convert]::ToBase64String($Bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_')
}

function New-TestJwt([string]$Issuer, [string]$Audience, [byte[]]$Key, [bool]$Expired = $false) {
    $now = [DateTimeOffset]::UtcNow.ToUnixTimeSeconds()
    $claims = @{ iss = $Issuer; aud = $Audience; sub = 'smoke-validation'; nbf = $now - 600; exp = $now + 300 }
    if ($Expired) { $claims.exp = $now - 120 }
    $header = Encode-Base64Url ([Text.Encoding]::UTF8.GetBytes('{"alg":"HS256","typ":"JWT"}'))
    $payload = Encode-Base64Url ([Text.Encoding]::UTF8.GetBytes(($claims | ConvertTo-Json -Compress)))
    $unsigned = "$header.$payload"
    $hmac = [Security.Cryptography.HMACSHA256]::new($Key)
    try { $signature = Encode-Base64Url ($hmac.ComputeHash([Text.Encoding]::ASCII.GetBytes($unsigned))) }
    finally { $hmac.Dispose() }
    "$unsigned.$signature"
}

try {
    Assert-That ($BaseUrl.IsAbsoluteUri -and $BaseUrl.Scheme -in @('http', 'https') -and $BaseUrl.Host -in @('localhost', '127.0.0.1') -and $BaseUrl.AbsolutePath -eq '/' -and -not $BaseUrl.UserInfo -and -not $BaseUrl.Query -and -not $BaseUrl.Fragment) 'Use a loopback base URL without credentials, path, query, or fragment.'
    $appRoot = $env:INVENTORY_SMOKE_APP_ROOT
    $hostProcessId = 0
    Assert-That ([int]::TryParse($env:INVENTORY_SMOKE_HOST_PID, [ref]$hostProcessId) -and $hostProcessId -gt 0 -and -not [string]::IsNullOrWhiteSpace($appRoot)) 'Set INVENTORY_SMOKE_HOST_PID and INVENTORY_SMOKE_APP_ROOT for the host you started.'
    $appRoot = (Resolve-Path -LiteralPath $appRoot).Path
    $process = Get-CimInstance Win32_Process -Filter "ProcessId = $hostProcessId"
    Assert-That ($null -ne $process -and $process.CommandLine.IndexOf($appRoot, [StringComparison]::OrdinalIgnoreCase) -ge 0) 'Host process command line must identify the intended application directory.'
    $listeners = @(Get-NetTCPConnection -LocalPort $BaseUrl.Port -State Listen)
    # IIS Express uses HTTP.sys (PID 4); verify its request queue belongs to our PID.
    if ($listeners.OwningProcess -contains 4) {
        $queues = (& netsh http show servicestate view=requestq verbose=yes) -join "`n"
        Assert-That ($LASTEXITCODE -eq 0) 'Cannot inspect HTTP.sys host ownership.'
        $ownedQueue = @($queues -split '(?m)(?=^Request queue name:)' | Where-Object {
            $_ -match "(?m)^\s*ID:\s*$hostProcessId(?:,|\s*$)" -and $_.IndexOf($BaseUrl.AbsoluteUri, [StringComparison]::OrdinalIgnoreCase) -ge 0
        })
        Assert-That ($ownedQueue.Count -eq 1) 'HTTP.sys URL must belong to the specified host process.'
    }
    else { Assert-That ($listeners.Count -gt 0 -and @($listeners | Where-Object OwningProcess -ne $hostProcessId).Count -eq 0) 'Port is not owned exclusively by the intended host.' }

    $connectionString = $env:INVENTORY_SMOKE_CONNECTION_STRING
    Assert-That (-not [string]::IsNullOrWhiteSpace($connectionString)) 'Set INVENTORY_SMOKE_CONNECTION_STRING to the isolated fixture.'
    $builder = [System.Data.Common.DbConnectionStringBuilder]::new()
    $builder.set_ConnectionString($connectionString)
    $server = [string]$builder['Data Source']
    $database = [string]$builder['Initial Catalog']
    Assert-That ($server -match '^\(LocalDB\)\\eShop(Baseline|Smoke)_[a-zA-Z0-9_]+$' -and $database -match '^eShop(Baseline|Smoke)_[a-zA-Z0-9_]+$' -and [string]$builder['Integrated Security'] -ieq 'True') 'Only named, integrated-security eShopBaseline_/eShopSmoke_ LocalDB fixtures are allowed.'
    $webConfig = Join-Path $appRoot 'Web.config'
    if (Test-Path -LiteralPath $webConfig) {
        [xml]$config = Get-Content -LiteralPath $webConfig -Raw
        $configuredConnection = [string]($config.configuration.connectionStrings.add | Where-Object name -eq InventoryContext).connectionString
    }
    else {
        $config = Get-Content -LiteralPath (Join-Path $appRoot 'appsettings.json') -Raw | ConvertFrom-Json -AsHashtable
        $configuredConnection = $config.ConnectionStrings.InventoryContext
    }
    Assert-That ($configuredConnection -ceq $connectionString) 'Host configuration must match the explicitly selected isolated fixture; do not use environment overrides.'
    $identity = & sqlcmd -S $server -d $database -E -l 15 -t 15 -b -h -1 -W -Q 'SET NOCOUNT ON; SELECT DB_NAME();'
    Assert-That ($LASTEXITCODE -eq 0 -and ($identity -join '').Trim() -ceq $database) 'Cannot verify isolated database identity.'
    Check 'preflight: intended local host and isolated database verified before writes' {}

    $userName = $env:INVENTORY_SMOKE_USERNAME
    $password = $env:INVENTORY_SMOKE_PASSWORD
    $keyText = $env:INVENTORY_SMOKE_SIGNING_KEY
    if ([string]::IsNullOrWhiteSpace($userName)) { $userName = Read-Host 'Fixture username' }
    if ([string]::IsNullOrWhiteSpace($password)) { $password = Read-Host 'Fixture password' -MaskInput }
    if ([string]::IsNullOrWhiteSpace($keyText)) { $keyText = Read-Host 'Fixture Base64 signing key' -MaskInput }
    try { $key = [Convert]::FromBase64String($keyText) }
    catch { throw 'Signing key must be Base64.' }
    Assert-That ($key.Length -ge 32) 'Signing key must contain at least 32 bytes.'
    $issuer = $env:INVENTORY_SMOKE_ISSUER
    $audience = $env:INVENTORY_SMOKE_AUDIENCE
    Assert-That (-not [string]::IsNullOrWhiteSpace($issuer) -and -not [string]::IsNullOrWhiteSpace($audience)) 'Set INVENTORY_SMOKE_ISSUER and INVENTORY_SMOKE_AUDIENCE to the host settings.'
    $login = Send-Request POST '/api/auth/login' (@{ UserName = $userName; Password = $password } | ConvertTo-Json -Compress)
    Check 'login: 200; exact snake_case keys and types; Bearer; expires_in=1800' {
        Expect-Status $login 200
        $json = Read-Json $login
        Assert-Keys $json @('access_token', 'token_type', 'expires_in')
        Assert-That ($json.access_token -is [string] -and $json.access_token -match '^[\w-]+\.[\w-]+\.[\w-]+$' -and $json.token_type -is [string] -and $json.token_type -ceq 'Bearer' -and ($json.expires_in -is [long] -or $json.expires_in -is [int]) -and $json.expires_in -eq 1800) 'Unexpected login values/types.'
        $script:token = $json.access_token
    }
    $before = Get-StockList
    Check 'list: 200 JSON array; PascalCase typed stock; ascending Id' {}
    Check 'missing ID: GET/PUT/DELETE 404; empty bodies and no Content-Type' {
        Expect-Empty (Send-Request GET "/api/stock/$missingId" -Bearer $token) 404
        Expect-Empty (Send-Request PUT "/api/stock/$missingId" (@{ Sku = $sku; Quantity = 1 } | ConvertTo-Json -Compress) -Bearer $token) 404
        Expect-Empty (Send-Request DELETE "/api/stock/$missingId" -Bearer $token) 404
    }
    Check 'wrong credentials: 401; empty body and no Content-Type' {
        Expect-Empty (Send-Request POST '/api/auth/login' (@{ UserName = $userName; Password = [guid]::NewGuid().ToString('N') } | ConvertTo-Json -Compress)) 401
    }
    foreach ($body in @('null', '{')) {
        Check "login null/malformed ($body): 400 exact null-request Message" {
            Expect-Error (Send-Request POST '/api/auth/login' $body) 'A login request is required.'
        }
    }
    Check 'login missing fields: 400 exact Message and request.UserName/request.Password errors' {
        Expect-Error (Send-Request POST '/api/auth/login' '{}') 'The request is invalid.' @{
            'request.UserName' = 'The UserName field is required.'
            'request.Password' = 'The Password field is required.'
        }
    }
    Check 'login invalid username (257 chars): 400 exact request.UserName length error' {
        Expect-Error (Send-Request POST '/api/auth/login' (@{ UserName = 'u' * 257; Password = $password } | ConvertTo-Json -Compress)) 'The request is invalid.' @{
            'request.UserName' = 'The field UserName must be a string with a maximum length of 256.'
        }
    }
    foreach ($case in @(@('GET', '/api/stock'), @('GET', "/api/stock/$missingId"), @('POST', '/api/stock'), @('PUT', "/api/stock/$missingId"), @('DELETE', "/api/stock/$missingId"))) {
        Check "anonymous $($case[0]) $($case[1]): 401 exact JSON Message and Bearer challenge" {
            Expect-Denied (Send-Request $case[0] $case[1] -Body $(if ($case[0] -in @('POST', 'PUT')) { @{ Sku = $sku; Quantity = 1 } | ConvertTo-Json -Compress } else { '' }))
        }
    }
    $wrongKey = [Security.Cryptography.RandomNumberGenerator]::GetBytes(32)
    $badTokens = [ordered]@{
        malformed = 'not-a-jwt'
        expired = (New-TestJwt $issuer $audience $key $true)
        'wrong signature' = (New-TestJwt $issuer $audience $wrongKey)
        'wrong issuer' = (New-TestJwt ($issuer + '-wrong') $audience $key)
        'wrong audience' = (New-TestJwt $issuer ($audience + '-wrong') $key)
    }
    # A positive control proves the crafted negative JWTs differ by their named defect.
    Check 'locally signed valid JWT control: authorized 200' {
        Expect-Status (Send-Request GET '/api/stock' -Bearer (New-TestJwt $issuer $audience $key)) 200
    }
    foreach ($name in $badTokens.Keys) {
        Check "$name JWT: 401 exact JSON Message and Bearer challenge" {
            Expect-Denied (Send-Request GET '/api/stock' -Bearer $badTokens[$name])
        }
    }
    $createdId = 0
    Check 'create: 201 typed PascalCase JSON; same-origin usable Location; persisted read' {
        $response = Send-Request POST '/api/stock' (@{ Sku = $sku; Quantity = 7 } | ConvertTo-Json -Compress) -Bearer $token
        Expect-Status $response 201
        $stock = Read-Json $response
        Assert-Stock $stock
        $script:createdId = [int]$stock.Id
        Assert-That ($stock.Sku -ceq $sku -and $stock.Quantity -eq 7) 'Created values differ.'
        $location = [uri]::new($BaseUrl, [string]$response.Headers['Location'])
        Assert-That ($location.GetLeftPart([UriPartial]::Authority) -ceq $BaseUrl.GetLeftPart([UriPartial]::Authority) -and $location.PathAndQuery -ceq "/api/stock/$createdId") 'Expected same-origin stock Location.'
        Expect-Record $createdId $sku 7
    }
    $invalidBodies = @(
        @{ Name = 'null'; Body = 'null'; Message = 'A stock record is required.'; Errors = @{} },
        @{ Name = 'malformed JSON'; Body = '{'; Message = 'A stock record is required.'; Errors = @{} },
        @{ Name = 'missing fields'; Body = '{}'; Message = 'The request is invalid.'; Errors = @{ 'request.Sku' = 'The Sku field is required.'; 'request.Quantity' = 'The Quantity field is required.' } },
        @{ Name = 'empty SKU and negative quantity'; Body = '{"Sku":"","Quantity":-1}'; Message = 'The request is invalid.'; Errors = @{ 'request.Sku' = 'The Sku field is required.'; 'request.Quantity' = 'The field Quantity must be between 0 and 2147483647.' } },
        @{ Name = 'overlong SKU'; Body = (@{ Sku = $overlongSku; Quantity = 1 } | ConvertTo-Json -Compress); Message = 'The request is invalid.'; Errors = @{ 'request.Sku' = 'The field Sku must be a string with a maximum length of 64.' } },
        @{ Name = 'negative quantity'; Body = (@{ Sku = $sku; Quantity = -1 } | ConvertTo-Json -Compress); Message = 'The request is invalid.'; Errors = @{ 'request.Quantity' = 'The field Quantity must be between 0 and 2147483647.' } }
    )
    foreach ($method in @('POST', 'PUT')) {
        $path = if ($method -eq 'POST') { '/api/stock' } else { "/api/stock/$createdId" }
        foreach ($case in $invalidBodies) {
            Check "$method $($case.Name): 400 exact Message/ModelState; no persisted changes" {
                Expect-Error (Send-Request $method $path $case.Body -Bearer $token) $case.Message $case.Errors
                Expect-Record $createdId $sku 7
                $current = Get-StockList
                Assert-That ($current.Count -eq $before.Count + 1) 'Invalid write changed stock count.'
            }
        }
    }
    Check 'XML Accept: 200 JSON with unchanged stock array' {
        $normal = Get-StockList
        $xmlRequested = Get-StockList 'application/xml'
        Assert-That (($normal | ConvertTo-Json -Depth 5 -Compress) -ceq ($xmlRequested | ConvertTo-Json -Depth 5 -Compress)) 'XML Accept changed the JSON data.'
    }
    Check 'update: 204 empty/no Content-Type; read confirms changed SKU and quantity' {
        Expect-Empty (Send-Request PUT "/api/stock/$createdId" (@{ Sku = $updatedSku; Quantity = 9 } | ConvertTo-Json -Compress) -Bearer $token) 204
        Expect-Record $createdId $updatedSku 9
    }
    Check 'delete: 204 empty/no Content-Type; subsequent read 404' {
        Expect-Empty (Send-Request DELETE "/api/stock/$createdId" -Bearer $token) 204
        Expect-Empty (Send-Request GET "/api/stock/$createdId" -Bearer $token) 404
    }
    Check 'final list: exact pre-run records preserved; no suite records remain' {
        $after = Get-StockList
        Assert-That (($before | ConvertTo-Json -Depth 5 -Compress) -ceq ($after | ConvertTo-Json -Depth 5 -Compress)) 'Final stock differs from pre-run snapshot.'
    }
}
catch {
    $failed = $true
    # Assertion messages are controlled above. Do not print arbitrary runtime errors.
    $safeMessage = if ($_.Exception.Data.Contains('SmokeAssertion')) { $_.Exception.Message } else { "Suite execution failed ($($_.Exception.GetType().Name), line $($_.InvocationInfo.ScriptLineNumber)); inspect configuration/tool availability without printing secrets." }
    Write-Output "FAIL $currentCheck (after $passed checks): $safeMessage"
}
finally {
    if ($token) {
        try {
            $remaining = @((Get-StockList) | Where-Object { $_.Sku -ceq $sku -or $_.Sku -ceq $updatedSku -or $_.Sku -ceq $overlongSku })
            foreach ($stock in $remaining) {
                Expect-Empty (Send-Request DELETE "/api/stock/$($stock.Id)" -Bearer $token) 204
                Expect-Empty (Send-Request GET "/api/stock/$($stock.Id)" -Bearer $token) 404
            }
            $leftovers = @((Get-StockList) | Where-Object { $_.Sku -ceq $sku -or $_.Sku -ceq $updatedSku -or $_.Sku -ceq $overlongSku })
            Assert-That ($leftovers.Count -eq 0) 'Suite-created stock remains.'
            Write-Output "CLEANUP PASS: $($remaining.Count) suite-owned records removed; none remain."
        }
        catch {
            $failed = $true
            Write-Output "CLEANUP FAIL: inspect only records with SKU prefix $prefix; no database reset was attempted."
        }
    }
}
if ($failed) { exit 1 }
Write-Output "PASS: $passed required scenario groups; cleanup verified."
exit 0
