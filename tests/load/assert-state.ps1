# Pure reconciliation shared by the live verifier and its corruption regression tests.
function Get-LoadStateErrors {
    param($Booking, [object[]] $Tickets, $Payments, [object[]] $Queues,
        [int] $RedisAvailable, [int] $RedisHolds, [int] $Capacity,
        [string] $Scenario, [string] $PaymentOutcome, $Metrics)

    $issues = [System.Collections.Generic.List[string]]::new()
    $rows = @($Booking.bookings)
    $warmupIds = @($Booking.warmupBookingIds)
    $mainTickets = @($Tickets | Where-Object { $_.BookingId -notin $warmupIds })
    $mainPayments = @($Payments.payments | Where-Object { $_.BookingId -notin $warmupIds })
    $ticketIndex = @{}
    $paymentIndex = @{}
    $bookingIds = @{}
    foreach ($ticket in $mainTickets) {
        if (-not $ticketIndex.ContainsKey($ticket.BookingId)) { $ticketIndex[$ticket.BookingId] = [System.Collections.Generic.List[object]]::new() }
        $ticketIndex[$ticket.BookingId].Add($ticket)
    }
    foreach ($payment in $mainPayments) {
        if (-not $paymentIndex.ContainsKey($payment.BookingId)) { $paymentIndex[$payment.BookingId] = [System.Collections.Generic.List[object]]::new() }
        $paymentIndex[$payment.BookingId].Add($payment)
    }
    foreach ($row in $rows) { $bookingIds[$row.BookingId] = $true }
    $confirmedQuantity = 0
    if ($null -eq $Booking.inventory -or $Booking.inventory.TotalQuantity -ne $Capacity) {
        $issues.Add('Fixture inventory is missing or its capacity changed.')
    }
    if ($rows.Count -eq 0) { $issues.Add('No measured bookings were created.') }

    foreach ($row in $rows) {
        $id = $row.BookingId
        $issued = @(); $charges = @()
        if ($ticketIndex.ContainsKey($id)) { $issued = @($ticketIndex[$id].ToArray()) }
        if ($paymentIndex.ContainsKey($id)) { $charges = @($paymentIndex[$id].ToArray()) }
        if ($row.Status -notin @('Confirmed', 'Cancelled', 'Expired')) {
            $issues.Add("Booking $id is still $($row.Status).")
        }
        if ($row.PaymentRequested -and -not $row.PaymentSettled) {
            $issues.Add("Payment for $id has not settled.")
        }
        if ($row.Status -eq 'Confirmed') {
            $confirmedQuantity += $row.Quantity
            if ($issued.Count -ne $row.Quantity) { $issues.Add("Wrong ticket count for $id.") }
            $numbers = @($issued | ForEach-Object { $_.TicketNumberInBookingItem } | Select-Object -Unique)
            if ($numbers.Count -ne $issued.Count) { $issues.Add("Duplicate ticket number for $id.") }
            foreach ($ticket in $issued) {
                if ($ticket.Status -ne 'Valid' -or $ticket.BookingItemId -ne $row.BookingItemId `
                    -or $ticket.EventId -ne '73000000-0000-0000-0000-000000000001' `
                    -or $ticket.TicketNumberInBookingItem -lt 1 -or $ticket.TicketNumberInBookingItem -gt $row.Quantity `
                    -or $ticket.UnitPrice -ne $row.UnitPrice -or $ticket.Currency -ne $row.Currency) {
                    $issues.Add("Invalid ticket identity/status/price for $id.")
                }
            }
            if (-not $row.PaymentRequested -or $charges.Count -ne 1 `
                -or $charges[0].Status -ne 'Succeeded' -or $null -ne $charges[0].RefundedAt) {
                $issues.Add("Confirmed booking $id has no unique successful, unrefunded payment.")
            }
        }
        elseif ($issued.Count -gt 0) { $issues.Add("Unconfirmed booking $id has tickets.") }

        foreach ($charge in $charges) {
            if ($charge.Status -eq 'Pending' -or $charge.Amount -ne ($row.Quantity * $row.UnitPrice) `
                -or $charge.Currency -ne $row.Currency `
                -or ($row.Status -ne 'Confirmed' -and $charge.Status -eq 'Succeeded' -and $null -eq $charge.RefundedAt)) {
                $issues.Add("Invalid or unsettled charge for $id.")
            }
        }
        if ($charges.Count -gt 1) { $issues.Add("Duplicate payment for $id.") }
        if ($Scenario -eq 'Checkout') {
            if ($row.Status -ne 'Confirmed') { $issues.Add("Checkout booking $id did not confirm.") }
        }
        else {
            if ($row.CustomerEmail -like 'release-abandon-*') {
                if ($row.Status -ne 'Expired' -or $row.PaymentRequested -or $charges.Count -ne 0) {
                    $issues.Add("Abandoned reservation $id has not expired cleanly.")
                }
            }
            elseif ($row.CustomerEmail -like 'release-cancel-*') {
                if ($row.Status -ne 'Cancelled' -or $row.PaymentRequested -or $charges.Count -ne 0) {
                    $issues.Add("Customer cancellation $id has not released its reservation cleanly.")
                }
            }
            elseif ($row.CustomerEmail -like 'release-pay-*') {
                $expectedStatus = if ($PaymentOutcome -eq 'Succeeded') { 'Confirmed' } else { 'Cancelled' }
                if ($row.Status -ne $expectedStatus -or $charges.Count -ne 1 `
                    -or $charges[0].Status -ne $PaymentOutcome -or $null -ne $charges[0].RefundedAt) {
                    $issues.Add("Payment cohort $id has not reached its planned outcome.")
                }
            }
            else { $issues.Add("Unknown customer cohort for $id.") }
        }
    }
    if ($confirmedQuantity -gt $Capacity) { $issues.Add('Confirmed quantity exceeds capacity.') }
    if ($Booking.inventory.AvailableQuantity -ne ($Capacity - $confirmedQuantity) `
        -or $Booking.inventory.AvailableQuantity -lt 0 -or $Booking.inventory.AvailableQuantity -gt $Capacity) {
        $issues.Add('Persistent inventory does not reconcile with confirmed quantity.')
    }
    if ($RedisAvailable -ne ($Capacity - $confirmedQuantity) -or $RedisHolds -ne 0) {
        $issues.Add('Redis stock or active holds do not reconcile with the settled inventory.')
    }
    foreach ($ticket in $mainTickets) {
        if (-not $bookingIds.ContainsKey($ticket.BookingId)) { $issues.Add('Orphan or misrouted ticket found.') }
    }
    foreach ($payment in $mainPayments) {
        if (-not $bookingIds.ContainsKey($payment.BookingId)) { $issues.Add('Orphan payment found.') }
    }
    if (@($mainTickets | ForEach-Object { $_.TicketId } | Select-Object -Unique).Count -ne $mainTickets.Count) {
        $issues.Add('Duplicate ticket IDs found.')
    }
    if ($Booking.pendingOutbox -ne 0 -or $Booking.pendingInbox -ne 0 -or $Payments.pendingOutbox -ne 0) {
        $issues.Add('Outbox/inbox work remains pending.')
    }
    if ($Booking.rejectedInbox -ne 0) { $issues.Add('Payment inbox contains rejected messages.') }
    foreach ($queueName in @('payments.payment-requested.v1', 'booking.payment-results.v1', 'tickets.booking-confirmed.v1')) {
        if ($queueName -notin @($Queues.name)) { $issues.Add("Expected queue $queueName is missing.") }
    }
    foreach ($queue in $Queues) {
        if ($queue.messages -ne 0 -or $queue.messages_ready -ne 0 -or $queue.messages_unacknowledged -ne 0) {
            $issues.Add("Queue $($queue.name) is not empty (including dead letters).")
        }
    }
    if ($Scenario -ne 'Checkout' -and $null -ne $Metrics) {
        $expectedCounts = @{
            release_reserved = $rows.Count
            release_paid = @($rows | Where-Object { $_.Status -eq 'Confirmed' }).Count
            release_abandoned = @($rows | Where-Object { $_.CustomerEmail -like 'release-abandon-*' }).Count
            release_cancelled = @($rows | Where-Object { $_.CustomerEmail -like 'release-cancel-*' }).Count
            release_payment_rejected = @($rows | Where-Object { $_.CustomerEmail -like 'release-pay-*' -and $_.Status -eq 'Cancelled' }).Count
        }
        foreach ($name in $expectedCounts.Keys) {
            if ($Metrics.$name.count -ne $expectedCounts[$name]) { $issues.Add("k6 metric $name does not match database state.") }
        }
    }
    return $issues.ToArray()
}
