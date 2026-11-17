param([string]$Path = (Join-Path $PSScriptRoot '../../.env'))
$ErrorActionPreference = 'Stop'
foreach ($line in Get-Content -LiteralPath $Path) {
    if ($line -match '^\s*#' -or [string]::IsNullOrWhiteSpace($line)) { continue }
    $parts = $line.Split('=',2)
    if ($parts.Count -ne 2) { throw 'Invalid environment line' }
    [Environment]::SetEnvironmentVariable($parts[0].Trim(), $parts[1], 'Process')
}
