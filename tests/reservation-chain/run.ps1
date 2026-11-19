param([switch] $SkipBuild)
$ErrorActionPreference = 'Stop'
$project = 'halsingelive-reservations-' + [Guid]::NewGuid().ToString('N').Substring(0, 8)
$resultPath = Join-Path $PSScriptRoot 'results'
$null = New-Item -ItemType Directory -Path $resultPath -Force
$composeArguments = @('compose', '--project-name', $project, '--file', (Join-Path $PSScriptRoot 'compose.yaml'))

function Invoke-TestCompose {
    param([string[]] $Arguments)
    $output = & docker @composeArguments @Arguments
    if ($LASTEXITCODE -ne 0) { throw "Compose failed: $($Arguments -join ' ') (exit $LASTEXITCODE)" }
    return $output
}
function Save-Text {
    param([string] $Name, [object] $Content)
    [IO.File]::WriteAllText((Join-Path $resultPath $Name), (@($Content) -join "`n") + "`n", [Text.UTF8Encoding]::new($false))
}
function Read-DatabaseState {
    return Invoke-TestCompose -Arguments @('exec', '-T', 'postgres', 'psql', '-U', 'reservationtest', '-d', 'booking', '-At', '-v', 'ON_ERROR_STOP=1', '-f', '/state.sql')
}

$passed = $false
try {
    $config = Invoke-TestCompose -Arguments @('config', '--format', 'json')
    Save-Text 'config.json' $config # This standalone fixture contains public test credentials only.
    $hashes = @{}
    foreach ($file in @('compose.yaml', 'verify.py', 'catalog.py', 'fixture.sql', 'state.sql', 'run.ps1',
        '../../src/frontend/customer-web/nginx/default.conf.template', '../../src/frontend/admin-web/nginx/default.conf.template')) {
        $hashes[$file] = (Get-FileHash -LiteralPath (Join-Path $PSScriptRoot $file) -Algorithm SHA256).Hash.ToLowerInvariant()
    }
    Save-Text 'run.json' (@{ startedAtUtc = [DateTime]::UtcNow.ToString('o'); project = $project
        commit = (& git -C $PSScriptRoot rev-parse HEAD); workingTreeDirty = @(& git -C $PSScriptRoot status --porcelain).Count -gt 0
        sourceSha256 = $hashes; catalog = 'synthetic HTTP fixture; Booking and gateway use production entry points'
    } | ConvertTo-Json -Depth 4)
    $startup = @('up', '--detach', '--wait', '--wait-timeout', '180')
    if (-not $SkipBuild) { $startup += '--build' }
    Invoke-TestCompose -Arguments $startup
    Invoke-TestCompose -Arguments @('exec', '-T', 'customer', 'nginx', '-t')
    Invoke-TestCompose -Arguments @('exec', '-T', 'admin', 'nginx', '-t')
    Invoke-TestCompose -Arguments @('exec', '-T', 'postgres', 'psql', '-U', 'reservationtest', '-d', 'booking', '-v', 'ON_ERROR_STOP=1', '-f', '/fixture.sql')
    $before = Read-DatabaseState
    Save-Text 'database-before.json' $before
    if (($before | ConvertFrom-Json).bookingCount -ne 0) { throw 'The fixture must start without bookings.' }
    foreach ($case in @('client', 'other', 'active', 'quantity')) {
        Invoke-TestCompose -Arguments @('run', '--rm', '--no-deps', $case)
    }
    Invoke-TestCompose -Arguments @('run', '--rm', '--no-deps', '--env', 'CASE=recovery', 'client')
    $after = Read-DatabaseState
    Save-Text 'database-after.json' $after
    $state = $after | ConvertFrom-Json
    if ($state.bookingCount -ne 9) { throw "Expected 9 bookings, found $($state.bookingCount)." }
    $expectedBookings = @(3, 1, 1, 2, 2)
    $expectedQuantities = @(3, 1, 1, 2, 10)
    for ($i = 0; $i -lt 5; $i++) {
        $inventory = $state.inventories[$i]
        if ($inventory.available -ne 100 -or $inventory.bookings -ne $expectedBookings[$i] -or $inventory.quantity -ne $expectedQuantities[$i]) {
            throw "Unexpected persistent stock or booking rows for inventory $($inventory.id)."
        }
    }
    if (@($state.bookings | Where-Object { $_.status -ne 'Reserved' }).Count -ne 0) { throw 'Unexpected booking status.' }
    $logs = Invoke-TestCompose -Arguments @('logs', '--no-color', '--no-log-prefix', 'customer', 'admin')
    $access = @($logs | Where-Object { $_.StartsWith('{"method":') } | ForEach-Object { $_ | ConvertFrom-Json })
    if ($access.Count -ne 12) { throw "Expected 12 privacy access records, found $($access.Count)." }
    foreach ($entry in $access) {
        $fields = @($entry.PSObject.Properties.Name | Sort-Object) -join ','
        if ($fields -ne 'bytes,duration,method,path,status' -or $entry.path -ne '/booking-api/CreateBooking') {
            throw 'Unexpected fields or query data in routine access log.'
        }
    }
    Save-Text 'access.json' (ConvertTo-Json -InputObject $access -Depth 4)
    foreach ($case in @('frequency', 'other', 'active', 'quantity', 'recovery')) {
        if (-not (Get-Content -Raw -LiteralPath (Join-Path $resultPath "$case.json") | ConvertFrom-Json).passed) {
            throw "Scenario $case did not pass."
        }
    }
    $passed = $true
    Write-Host 'PASS: real proxy chain, 3/60s, other IP, recovery, active/ticket quotas, stock, database and privacy access logs.'
}
finally {
    Save-Text 'outcome.json' (@{ passed = $passed; finishedAtUtc = [DateTime]::UtcNow.ToString('o') } | ConvertTo-Json)
    try { Save-Text 'services.log' (Invoke-TestCompose -Arguments @('logs', '--no-color')) }
    finally { Invoke-TestCompose -Arguments @('down', '--remove-orphans') }
    Write-Host "Evidence: $resultPath"
}
