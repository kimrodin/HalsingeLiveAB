$ErrorActionPreference = 'Stop'

$repoRoot = Split-Path -Parent $PSScriptRoot
$composeFile = Join-Path $repoRoot 'src/Compose.yaml'
$projectFile = Join-Path $repoRoot 'src/services/EventCatalog/EventCatalog.csproj'

function Invoke-Checked {
    param(
        [string] $Command,
        [string[]] $Arguments
    )

    & $Command @Arguments
    if ($LASTEXITCODE -ne 0) {
        throw "$Command failed with exit code $LASTEXITCODE."
    }
}

Get-Command docker -ErrorAction Stop | Out-Null
Get-Command dotnet -ErrorAction Stop | Out-Null
Invoke-Checked dotnet @('ef', '--version')

Write-Host 'Starting PostgreSQL and waiting until it is ready...'
Invoke-Checked docker @('compose', '-f', $composeFile, 'up', '-d', '--wait', '--wait-timeout', '120', 'eventcatalog-db')

$previousConnectionString = [Environment]::GetEnvironmentVariable('ConnectionStrings__EventCatalog', 'Process')
try {
    $env:ConnectionStrings__EventCatalog = 'Host=localhost;Port=5432;Database=eventcatalog_dev;Username=postgres;Password=postgres'
    Write-Host 'Applying EF Core migrations...'
    Invoke-Checked dotnet @('ef', 'database', 'update', '--context', 'EventCatalog.Data.EventCatalogDbContext', '--project', $projectFile, '--startup-project', $projectFile)
}
finally {
    [Environment]::SetEnvironmentVariable('ConnectionStrings__EventCatalog', $previousConnectionString, 'Process')
}

Write-Host 'Building and starting all services...'
Invoke-Checked docker @('compose', '-f', $composeFile, 'up', '-d', '--build')

Write-Host 'Swagger: http://localhost:8080/swagger'
Write-Host 'Admin:   http://localhost:5173'
