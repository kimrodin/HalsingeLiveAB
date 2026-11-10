param(
    [Parameter(Mandatory)] [string[]] $ComposePrefix,
    [Parameter(Mandatory)] [string] $ResultPath,
    [Parameter(Mandatory)] [int] $Capacity,
    [Parameter(Mandatory)] [string] $Scenario,
    [string] $PaymentOutcome = 'Succeeded',
    [int] $TimeoutSeconds = 720
)

$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'assert-state.ps1')
function Read-ComposeJson {
    param([string[]] $Arguments)
    $output = & docker @ComposePrefix @Arguments
    if ($LASTEXITCODE -ne 0) { throw 'Could not read verification state from the isolated stack.' }
    $parsed = ($output -join "`n") | ConvertFrom-Json
    # Windows PowerShell keeps a root JSON array as one pipeline item. Enumerate it explicitly.
    foreach ($entry in $parsed) { Write-Output $entry }
}

$summaryPath = Join-Path $ResultPath 'summary.json'
if (-not (Test-Path -LiteralPath $summaryPath)) { throw 'k6 did not save summary.json; final verification cannot reconcile the workload.' }
$metrics = (Get-Content -LiteralPath $summaryPath -Raw | ConvertFrom-Json).metrics
if ($Scenario -ne 'Checkout') {
    foreach ($name in @('release_reserved', 'release_paid', 'release_abandoned', 'release_cancelled', 'release_payment_rejected')) {
        if ($null -eq $metrics.$name.count) { throw "k6 summary is missing $name; final verification cannot reconcile the workload." }
    }
}
$deadline = [DateTime]::UtcNow.AddSeconds($TimeoutSeconds)
$lastProgress = [DateTime]::MinValue
$consecutivePasses = 0
do {
    $booking = Read-ComposeJson -Arguments @('exec', '-T', 'postgres', 'psql', '-XAt', '-U', 'loadtest', '-d', 'booking', '-v', 'ON_ERROR_STOP=1', '-f', '/audit-booking.sql')
    $payments = Read-ComposeJson -Arguments @('exec', '-T', 'postgres', 'psql', '-XAt', '-U', 'loadtest', '-d', 'payments', '-v', 'ON_ERROR_STOP=1', '-f', '/audit-payments.sql')
    $tickets = @(Read-ComposeJson -Arguments @('exec', '-T', 'postgres', 'psql', '-XAt', '-U', 'loadtest', '-d', 'tickets', '-v', 'ON_ERROR_STOP=1', '-f', '/audit-tickets.sql'))
    $queues = @(Read-ComposeJson -Arguments @('exec', '-T', 'booking', 'curl', '--fail', '--silent', '--show-error', '--max-time', '5', '-u', 'loadtest:loadtest', 'http://rabbitmq:15672/api/queues'))
    $stockKey = 'inventory:73000000000000000000000000000001:72000000000000000000000000000001'
    $redisAvailable = & docker @ComposePrefix exec -T redis redis-cli --raw GET "${stockKey}:available"
    if ($LASTEXITCODE -ne 0 -or "$redisAvailable" -notmatch '^\d+$') { throw 'Redis inventory could not be read.' }
    $redisHolds = & docker @ComposePrefix exec -T redis redis-cli --raw ZCARD "${stockKey}:reservations"
    if ($LASTEXITCODE -ne 0 -or "$redisHolds" -notmatch '^\d+$') { throw 'Redis reservation index could not be read.' }

    $issues = @(Get-LoadStateErrors -Booking $booking -Tickets $tickets -Payments $payments -Queues $queues `
        -RedisAvailable ([int] $redisAvailable) -RedisHolds ([int] $redisHolds) -Capacity $Capacity `
        -Scenario $Scenario -PaymentOutcome $PaymentOutcome -Metrics $metrics)
    if ($issues.Count -eq 0) { $consecutivePasses++ } else { $consecutivePasses = 0 }
    $report = @{
        checkedAtUtc = [DateTime]::UtcNow.ToString('o'); passed = ($consecutivePasses -ge 2)
        consecutivePasses = $consecutivePasses; issues = $issues
        booking = $booking; payments = $payments; tickets = $tickets; queues = $queues
        redisAvailable = [int] $redisAvailable; redisHolds = [int] $redisHolds
    }
    $report | ConvertTo-Json -Depth 12 | Set-Content -LiteralPath (Join-Path $ResultPath 'verification.json') -Encoding utf8
    if ($consecutivePasses -ge 2) {
        Write-Host 'Verification passed: bookings, payments, tickets, SQL/Redis inventory, and queues reconcile.'
        return
    }
    if (([DateTime]::UtcNow - $lastProgress).TotalSeconds -ge 30) {
        if ($issues.Count -eq 0) { Write-Host 'State reconciles; waiting for a second consistent observation...' }
        else { Write-Host "Waiting for settlement/expiry: $($issues.Count) checks pending; $($issues | Select-Object -First 2)" }
        $lastProgress = [DateTime]::UtcNow
    }
    Start-Sleep -Seconds 5
} while ([DateTime]::UtcNow -lt $deadline)
throw "Final verification timed out: $($issues -join ' ') See verification.json."
