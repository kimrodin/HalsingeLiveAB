param(
    [ValidateSet('Checkout', 'Release', 'Sustained')] [string] $Scenario = 'Checkout',
    [ValidateRange(1, 100)] [int] $Vus = 2,
    [ValidateRange(1, 1000000)] [int] $Iterations = 10,
    [ValidateRange(0, 1000000)] [int] $Capacity = 0,
    [ValidateRange(1, 1000)] [int] $BurstRate = 30,
    [ValidateRange(1, 3600)] [int] $BurstSeconds = 20,
    [ValidateRange(1, 1000)] [int] $TailRate = 10,
    [ValidateRange(1, 3600)] [int] $TailSeconds = 40,
    [ValidateRange(0, 3600)] [int] $RampSeconds = 0,
    [ValidateRange(0, 300)] [int] $PreSaleSeconds = 30,
    [ValidateRange(1, 100)] [int] $BrowseRate = 5,
    [ValidateRange(1, 5000)] [int] $PreAllocatedVus = 128,
    [ValidateRange(0, 99)] [int] $AbandonPercent = 10,
    [ValidateRange(0, 99)] [int] $CancelPercent = 10,
    [ValidateRange(1, 10)] [int] $MaxQuantity = 3,
    [ValidateRange(0, 60)] [int] $ThinkMaxSeconds = 3,
    [ValidateRange(5, 120)] [int] $CheckoutTimeoutSeconds = 60,
    [ValidateSet('Succeeded', 'Failed', 'TimedOut')] [string] $PaymentOutcome = 'Succeeded',
    [ValidateRange(0, 30000)] [int] $PaymentDelayMs = 500,
    [ValidateRange(1, 30000)] [int] $PaymentTimeoutMs = 5000,
    [ValidateRange(5, 1800)] [int] $VerificationTimeoutSeconds = 720,
    [ValidateRange(1, 300000)] [int] $HttpP95Ms = 500,
    [ValidateRange(1, 300000)] [int] $HttpP99Ms = 1000,
    [ValidateRange(1, 300000)] [int] $CheckoutP95Ms = 15000,
    [ValidateRange(1, 300000)] [int] $CheckoutP99Ms = 20000,
    [ValidateRange(0, 1)] [double] $MaxHttpErrorRate = 0,
    [switch] $SkipBuild
)

$ErrorActionPreference = 'Stop'
if (-not (Get-Command docker -ErrorAction SilentlyContinue)) { throw 'Install and start Docker Desktop first.' }
if ($AbandonPercent + $CancelPercent -ge 100) { throw 'AbandonPercent + CancelPercent must be below 100 so some customers pay.' }
if ($PaymentDelayMs -gt $PaymentTimeoutMs -and $PaymentOutcome -ne 'TimedOut') {
    throw 'PaymentDelayMs exceeding PaymentTimeoutMs forces TimedOut. Select that outcome explicitly.'
}
if ($Scenario -eq 'Checkout' -and $PaymentOutcome -ne 'Succeeded') { throw 'Use Release or Sustained to test payment failures.' }
if ($Scenario -eq 'Checkout' -and $RampSeconds -ne 0) { throw 'Use Release or Sustained for ramped traffic.' }
if ($HttpP99Ms -lt $HttpP95Ms -or $CheckoutP99Ms -lt $CheckoutP95Ms) { throw 'A p99 budget must be greater than or equal to its p95 budget.' }
if ($Capacity -eq 0) {
    $Capacity = switch ($Scenario) { 'Checkout' { 1000 }; 'Release' { 100 }; 'Sustained' { 100000 } }
}
if ($Scenario -eq 'Checkout' -and $Capacity -lt $Iterations + 1) { throw 'Checkout requires Capacity >= Iterations + 1 (including warm-up).' }

# Every run owns its network and disk-backed volumes, including during concurrent runs.
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
$rampAttempts = [Math]::Max(0, [Math]::Ceiling($BurstRate * $RampSeconds / 2.0) - 1)
$expectedAttempts = $rampAttempts + $BurstRate * $BurstSeconds + $TailRate * $TailSeconds
$sourceHashes = @{}
foreach ($file in (Get-ChildItem -LiteralPath $PSScriptRoot -File | Where-Object { $_.Extension -in @('.js', '.ps1', '.sql', '.yaml') })) {
    $sourceHashes[$file.Name] = (Get-FileHash -Algorithm SHA256 -LiteralPath $file.FullName).Hash.ToLowerInvariant()
}
@{
    startedAtUtc = [DateTime]::UtcNow.ToString('o'); commit = $commit; workingTreeDirty = $dirty
    scenario = $Scenario; capacity = $Capacity; virtualUsers = $Vus; checkouts = $Iterations
    plannedReleaseAttempts = $expectedAttempts; burstRate = $BurstRate; burstSeconds = $BurstSeconds
    tailRate = $TailRate; tailSeconds = $TailSeconds; preSaleSeconds = $PreSaleSeconds; browseRate = $BrowseRate
    rampSeconds = $RampSeconds; plannedRampAttempts = $rampAttempts
    preAllocatedVusPerPhase = $PreAllocatedVus; abandonPercent = $AbandonPercent; cancelPercent = $CancelPercent
    maxQuantity = $MaxQuantity; thinkMaxSeconds = $ThinkMaxSeconds; checkoutTimeoutSeconds = $CheckoutTimeoutSeconds
    warmupCheckouts = 1; warmupUsesSeparateEvent = ($Scenario -ne 'Checkout'); k6Image = 'grafana/k6:1.3.0'
    paymentOutcome = $PaymentOutcome; paymentDelayMs = $PaymentDelayMs; paymentTimeoutMs = $PaymentTimeoutMs
    verificationTimeoutSeconds = $VerificationTimeoutSeconds; storage = 'disk-backed isolated volumes (Redis AOF enabled)'
    databaseMaxConnections = 100; serviceMaxPoolSize = 20; composeProject = $project
    httpP95Ms = $HttpP95Ms; httpP99Ms = $HttpP99Ms; checkoutP95Ms = $CheckoutP95Ms; checkoutP99Ms = $CheckoutP99Ms
    maxHttpErrorRate = $MaxHttpErrorRate; sourceSha256 = $sourceHashes
} | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath (Join-Path $resultPath 'run.json') -Encoding utf8

try {
    Write-Host '[1/4] Building and starting an isolated stack with disk-backed storage...'
    $startup = @('up', '--detach', '--wait', '--wait-timeout', '180')
    if (-not $SkipBuild) { $startup += '--build' }
    Invoke-Compose -ComposeArguments $startup
    $managementAddress = Invoke-Compose -ComposeArguments @('port', 'rabbitmq', '15672')
    Write-Host "RabbitMQ UI: http://$managementAddress/#/queues"
    Write-Host 'Username: loadtest | Password: loadtest (available until cleanup)'
    Invoke-Compose -ComposeArguments @('exec', '-T', 'postgres', 'psql', '-U', 'loadtest', '-d', 'loadtest',
        '-v', 'ON_ERROR_STOP=1', '-v', "capacity=$Capacity", '-f', '/fixture.sql')
    Invoke-Compose -ComposeArguments @('exec', '-T', 'booking', 'curl', '--fail', '--silent', '--show-error',
        '--output', '/dev/null', '--max-time', '5', '--retry', '15', '--retry-connrefused', '--retry-delay', '1', 'http://api-gateway:8080/health')

    if ($Scenario -ne 'Checkout') {
        Write-Host 'Warming the messaging path using a separate one-ticket event...'
        Invoke-Compose -ComposeArguments @('run', '--rm', '--no-deps', '--volume', "${resultPath}:/results",
            '--env', 'VUS=1', '--env', 'ITERATIONS=1', '--env', 'SKIP_SETUP_WARMUP=true', '--env', 'WARMUP_ONLY=true',
            '--env', 'INVENTORY_ID=74000000-0000-0000-0000-000000000002',
            'k6', 'run', '--summary-export=/results/warmup.json', '/scripts/checkout.js')
    }
    Invoke-Compose -ComposeArguments @('exec', '-T', 'postgres', 'psql', '-U', 'loadtest', '-d', 'payments',
        '-v', 'ON_ERROR_STOP=1', '-v', "payment_outcome=$PaymentOutcome", '-v', "payment_delay=$PaymentDelayMs",
        '-v', "payment_timeout=$PaymentTimeoutMs", '-f', '/payment-policy.sql')

    Write-Host "[2/4] Running $Scenario (capacity $Capacity)..."
    $testArguments = @('run', '--rm', '--no-deps', '--volume', "${resultPath}:/results")
    $environment = @{
        VUS = $Vus; ITERATIONS = $Iterations; PRESALE_SECONDS = $PreSaleSeconds
        BURST_RATE = $BurstRate; BURST_SECONDS = $BurstSeconds; TAIL_RATE = $TailRate; TAIL_SECONDS = $TailSeconds
        RAMP_SECONDS = $RampSeconds
        BROWSE_RATE = $BrowseRate; PREALLOCATED_VUS = $PreAllocatedVus
        ABANDON_PERCENT = $AbandonPercent; CANCEL_PERCENT = $CancelPercent; MAX_QUANTITY = $MaxQuantity
        THINK_MAX_SECONDS = $ThinkMaxSeconds; CHECKOUT_TIMEOUT_SECONDS = $CheckoutTimeoutSeconds
        PAYMENT_OUTCOME = $PaymentOutcome; EXPECT_SOLD_OUT = ($Scenario -eq 'Release').ToString().ToLowerInvariant()
        HTTP_P95_MS = $HttpP95Ms; HTTP_P99_MS = $HttpP99Ms
        CHECKOUT_P95_MS = $CheckoutP95Ms; CHECKOUT_P99_MS = $CheckoutP99Ms
        MAX_HTTP_ERROR_RATE = $MaxHttpErrorRate.ToString([Globalization.CultureInfo]::InvariantCulture)
    }
    foreach ($key in $environment.Keys) { $testArguments += @('--env', "$key=$($environment[$key])") }
    $script = if ($Scenario -eq 'Checkout') { 'checkout.js' } else { 'release.js' }
    $testArguments += @('k6', 'run', '--summary-export=/results/summary.json', "/scripts/$script")
    & docker @compose @testArguments
    $testExitCode = $LASTEXITCODE

    Write-Host '[3/4] Verifying terminal bookings, payments, tickets, inventory and drained queues...'
    if ($Scenario -ne 'Checkout' -and $AbandonPercent -gt 0) {
        Write-Host 'Abandoned reservations retain their real ten-minute lifetime; progress is printed while waiting.'
    }
    $verificationFailure = $null
    try {
        & (Join-Path $PSScriptRoot 'verify.ps1') -ComposePrefix $compose -ResultPath $resultPath `
            -Capacity $Capacity -Scenario $Scenario -PaymentOutcome $PaymentOutcome -TimeoutSeconds $VerificationTimeoutSeconds
    }
    catch { $verificationFailure = $_.Exception.Message }
    @{
        k6ExitCode = $testExitCode; verificationPassed = ($null -eq $verificationFailure)
        verificationFailure = $verificationFailure
        passed = ($testExitCode -eq 0 -and $null -eq $verificationFailure)
    } | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $resultPath 'outcome.json') -Encoding utf8
    & docker @compose images --format json | Set-Content -LiteralPath (Join-Path $resultPath 'images.json') -Encoding utf8
    if ($testExitCode -ne 0 -or $null -ne $verificationFailure) {
        throw "Load test failed (k6 exit $testExitCode). $verificationFailure See $resultPath"
    }
}
finally {
    # Keep diagnostics on success, test failure, and startup failure.
    try {
        & docker @compose logs --no-color 2>&1 | Out-File -LiteralPath (Join-Path $resultPath 'services.log') -Encoding utf8
    }
    finally {
        Write-Host "[4/4] Removing only Compose project $project and its temporary volumes..."
        & docker @compose down --volumes --remove-orphans
        if ($LASTEXITCODE -ne 0) { Write-Warning "Cleanup failed. Retry: docker compose -p $project -f tests/load/compose.yaml down --volumes" }
        Write-Host "Results: $resultPath"
    }
}
