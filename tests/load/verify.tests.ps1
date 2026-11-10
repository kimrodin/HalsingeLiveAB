$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'assert-state.ps1')

function New-State {
    $booking = @{
        inventory = @{ TotalQuantity = 2; AvailableQuantity = 1 }; warmupBookingIds = @()
        pendingOutbox = 0; pendingInbox = 0; rejectedInbox = 0
        bookings = @(@{ BookingId = 'b'; Status = 'Confirmed'; CustomerEmail = 'release-pay-burst-1@example.com'
            PaymentRequested = $true; PaymentSettled = $true; BookingItemId = 'item'; Quantity = 1
            UnitPrice = 100; Currency = 'SEK' })
    }
    $state = @{
        Booking = $booking; Capacity = 2; Scenario = 'Release'; PaymentOutcome = 'Succeeded'
        RedisAvailable = 1; RedisHolds = 0; Metrics = $null
        Tickets = @(@{ BookingId = 'b'; BookingItemId = 'item'; TicketId = 't'; Status = 'Valid'
            EventId = '73000000-0000-0000-0000-000000000001'; TicketNumberInBookingItem = 1; UnitPrice = 100; Currency = 'SEK' })
        Payments = @{ pendingOutbox = 0; payments = @(@{ BookingId = 'b'; Status = 'Succeeded'; RefundedAt = $null; Amount = 100; Currency = 'SEK' }) }
        Queues = @('payments.payment-requested.v1', 'booking.payment-results.v1', 'tickets.booking-confirmed.v1') | ForEach-Object {
            @{ name = $_; messages = 0; messages_ready = 0; messages_unacknowledged = 0 }
        }
    }
    # Match real ConvertFrom-Json objects and preserve a splattable outer argument map.
    $jsonState = $state | ConvertTo-Json -Depth 10 | ConvertFrom-Json
    $arguments = @{}
    foreach ($property in $jsonState.PSObject.Properties) { $arguments[$property.Name] = $property.Value }
    return $arguments
}

$cases = @(
    @{ Name = 'consistent purchase'; Mutate = {}; Expected = $null },
    @{ Name = 'expired abandoned reservation'; Mutate = { param($s)
        $s.Booking.bookings[0].CustomerEmail = 'release-abandon-burst-0@example.com'
        $s.Booking.bookings[0].Status = 'Expired'; $s.Booking.bookings[0].PaymentRequested = $false
        $s.Booking.bookings[0].PaymentSettled = $false; $s.Tickets = @(); $s.Payments.payments = @()
        $s.Booking.inventory.AvailableQuantity = 2; $s.RedisAvailable = 2
    }; Expected = $null },
    @{ Name = 'planned declined payment'; Mutate = { param($s)
        $s.PaymentOutcome = 'Failed'; $s.Booking.bookings[0].Status = 'Cancelled'
        $s.Payments.payments[0].Status = 'Failed'; $s.Tickets = @()
        $s.Booking.inventory.AvailableQuantity = 2; $s.RedisAvailable = 2
    }; Expected = $null },
    @{ Name = 'separate warm-up is excluded'; Mutate = { param($s)
        $s.Booking.warmupBookingIds = @('warmup')
        $s.Tickets += [pscustomobject]@{ BookingId = 'warmup'; TicketId = 'warm-ticket' }
        $s.Payments.payments += [pscustomobject]@{ BookingId = 'warmup'; Status = 'Succeeded' }
    }; Expected = $null },
    @{ Name = 'overselling'; Mutate = { param($s) $s.Capacity = 0 }; Expected = 'Confirmed quantity exceeds capacity' },
    @{ Name = 'missing ticket'; Mutate = { param($s) $s.Tickets = @() }; Expected = 'Wrong ticket count' },
    @{ Name = 'duplicate ticket'; Mutate = { param($s) $s.Tickets += $s.Tickets[0] }; Expected = 'Duplicate ticket' },
    @{ Name = 'wrong ticket event'; Mutate = { param($s) $s.Tickets[0].EventId = 'wrong' }; Expected = 'Invalid ticket identity' },
    @{ Name = 'abandoned hold still active'; Mutate = { param($s)
        $s.Booking.bookings[0].CustomerEmail = 'release-abandon-burst-0@example.com'; $s.Booking.bookings[0].Status = 'Reserved'
    }; Expected = 'has not expired cleanly' },
    @{ Name = 'Redis stock leaked'; Mutate = { param($s) $s.RedisAvailable = 0 }; Expected = 'Redis stock' },
    @{ Name = 'pending outbox'; Mutate = { param($s) $s.Payments.pendingOutbox = 1 }; Expected = 'Outbox/inbox work remains' },
    @{ Name = 'dead-lettered message'; Mutate = { param($s)
        $s.Queues += @{ name = 'payment-request.dead'; messages = 1; messages_ready = 1; messages_unacknowledged = 0 }
    }; Expected = 'including dead letters' },
    @{ Name = 'unknown payment'; Mutate = { param($s)
        $s.Payments.payments += @{ BookingId = 'unknown'; Status = 'Succeeded'; RefundedAt = $null; Amount = 100; Currency = 'SEK' }
    }; Expected = 'Orphan payment' },
    @{ Name = 'missing payment'; Mutate = { param($s) $s.Payments.payments = @() }; Expected = 'no unique successful' },
    @{ Name = 'counter disagrees with database'; Mutate = { param($s)
        $s.Metrics = [pscustomobject]@{ release_reserved = [pscustomobject]@{ count = 2 } }
    }; Expected = 'k6 metric release_reserved does not match database state' },
    @{ Name = 'missing expected queue'; Mutate = { param($s) $s.Queues = @() }; Expected = 'Expected queue' }
)
foreach ($case in $cases) {
    $state = New-State
    & $case.Mutate $state
    $issues = @(Get-LoadStateErrors @state)
    if ($null -eq $case.Expected) {
        if ($issues.Count -ne 0) { throw "$($case.Name): $($issues -join '; ')" }
    }
    elseif (-not ($issues -match [regex]::Escape($case.Expected))) {
        throw "$($case.Name): expected '$($case.Expected)', got '$($issues -join '; ')'."
    }
    Write-Host "PASS: $($case.Name)"
}
