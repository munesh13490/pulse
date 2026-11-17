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
Write-Output 'Created ignored .env with random local secrets. Retrieve the initial password locally and remove Bootstrap__Password after bootstrap.'
