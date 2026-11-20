$ErrorActionPreference='Stop'
$target=Join-Path $PSScriptRoot '../../.env'
if(Test-Path -LiteralPath $target) { throw '.env already exists. Refusing to replace local secrets.' }
function New-Secret { [Convert]::ToHexString([Security.Cryptography.RandomNumberGenerator]::GetBytes(32)).ToLowerInvariant() }
$admin=New-Secret
$app=New-Secret
$auth=New-Secret
$jwt=New-Secret
$hmac=New-Secret
$password=New-Secret
$settingsPath=Join-Path $PSScriptRoot '../../backend/Pulse.Api/appsettings.json'
$settings=if(Test-Path -LiteralPath $settingsPath) {
    Get-Content -LiteralPath $settingsPath -Raw | ConvertFrom-Json -AsHashtable
} else { @{} }
$settings['ConnectionStrings'] = @{
    App = "Host=localhost;Database=pulse;Username=pulse_app;Password=$app"
    Auth = "Host=localhost;Database=pulse;Username=pulse_auth;Password=$auth"
    Migration = "Host=localhost;Database=pulse;Username=postgres;Password=$admin"
    Redis = 'localhost:6379,abortConnect=false'
}
@"
POSTGRES_PASSWORD=$admin
PULSE_APP_PASSWORD=$app
PULSE_AUTH_PASSWORD=$auth
ConnectionStrings__App=Host=localhost;Database=pulse;Username=pulse_app;Password=$app
ConnectionStrings__Auth=Host=localhost;Database=pulse;Username=pulse_auth;Password=$auth
ConnectionStrings__Migration=Host=localhost;Database=pulse;Username=postgres;Password=$admin
ConnectionStrings__Maintenance=Host=localhost;Database=pulse;Username=postgres;Password=$admin
ConnectionStrings__Redis=localhost:6379,abortConnect=false
Secrets__JwtSigningKey=$jwt
Secrets__ApiKeyHmacKey=$hmac
Bootstrap__Username=local-admin
Bootstrap__Password=$password
ASPNETCORE_URLS=http://localhost:5080
"@ | Set-Content -LiteralPath $target
$settings | ConvertTo-Json -Depth 32 | Set-Content -LiteralPath $settingsPath
Write-Output 'Created ignored .env and configured API connection strings in appsettings.json. Retrieve the initial password from .env and remove Bootstrap__Password after bootstrap.'
