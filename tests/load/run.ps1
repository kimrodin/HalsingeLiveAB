param(
    [ValidateRange(1, 100)] [int] $Vus = 2,
    [ValidateRange(1, 999)] [int] $Iterations = 10
)

$ErrorActionPreference = 'Stop'
if (-not (Get-Command docker -ErrorAction SilentlyContinue)) { throw 'Install and start Docker Desktop first.' }

# A unique project gives every run its own network and disposable databases.
$project = 'halsingelive-load-' + [Guid]::NewGuid().ToString('N').Substring(0, 8)
$compose = @('compose', '--project-name', $project, '--file', (Join-Path $PSScriptRoot 'compose.yaml'))
$resultPath = Join-Path $PSScriptRoot ('results/' + (Get-Date -Format 'yyyyMMdd-HHmmss') + '-' + $project.Substring($project.Length - 8))
$null = New-Item -ItemType Directory -Path $resultPath -Force

function Invoke-Compose {
    param([string[]] $ComposeArguments)
    & docker @compose @ComposeArguments
    if ($LASTEXITCODE -ne 0) { throw "Docker Compose failed (exit $LASTEXITCODE)." }
}

$commit = & git -C $PSScriptRoot rev-parse HEAD
$dirty = @(& git -C $PSScriptRoot status --porcelain).Count -gt 0
@{
    startedAtUtc = [DateTime]::UtcNow.ToString('o')
    commit = $commit
    workingTreeDirty = $dirty
    virtualUsers = $Vus
    checkouts = $Iterations
    warmupCheckouts = 1
    k6Image = 'grafana/k6:1.3.0'
    paymentOutcome = 'Succeeded'
    paymentDelayMs = 500
    paymentTimeoutMs = 5000
    databaseMaxConnections = 100
    serviceMaxPoolSize = 20
    composeProject = $project
} | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $resultPath 'run.json') -Encoding utf8

try {
    Write-Host '[1/3] Building and starting an isolated checkout stack...'
    Invoke-Compose -ComposeArguments @('up', '--detach', '--build', '--wait', '--wait-timeout', '180')
    $managementAddress = Invoke-Compose -ComposeArguments @('port', 'rabbitmq', '15672')
    Write-Host "RabbitMQ UI: http://$managementAddress/#/queues"
    Write-Host 'Username: loadtest | Password: loadtest (available until this run finishes)'
    Invoke-Compose -ComposeArguments @('exec', '-T', 'postgres', 'psql', '-U', 'loadtest', '-d', 'loadtest', '-v', 'ON_ERROR_STOP=1', '-f', '/fixture.sql')
    Invoke-Compose -ComposeArguments @('exec', '-T', 'booking', 'curl', '--fail', '--silent', '--show-error', '--output', '/dev/null', '--max-time', '5', '--retry', '15', '--retry-connrefused', '--retry-delay', '1', 'http://api-gateway:8080/health')

    Write-Host "[2/3] Warming up, then running $Iterations checkouts with $Vus virtual users..."
    & docker @compose run --rm --no-deps --volume "${resultPath}:/results" `
        --env "VUS=$Vus" --env "ITERATIONS=$Iterations" k6 `
        run --summary-export=/results/summary.json /scripts/checkout.js
    $testExitCode = $LASTEXITCODE
    & docker @compose images --format json | Set-Content -LiteralPath (Join-Path $resultPath 'images.json') -Encoding utf8
    & docker @compose logs --no-color | Out-File -LiteralPath (Join-Path $resultPath 'services.log') -Encoding utf8
    if ($testExitCode -ne 0) { throw "Load test failed (exit $testExitCode). See $resultPath" }
}
catch {
    # Preserve service errors before cleanup so failures can be inspected.
    & docker @compose logs --no-color 2>&1 | Out-File -LiteralPath (Join-Path $resultPath 'services.log') -Encoding utf8
    throw
}
finally {
    Write-Host "[3/3] Removing this run's containers and temporary data..."
    & docker @compose down --volumes --remove-orphans
    if ($LASTEXITCODE -ne 0) { Write-Warning "Cleanup failed. Retry: docker compose -p $project -f tests/load/compose.yaml down --volumes" }
    Write-Host "Results: $resultPath"
}
