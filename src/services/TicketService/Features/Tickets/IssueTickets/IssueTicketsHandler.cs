using Microsoft.EntityFrameworkCore;
using Npgsql;
using TicketService.Data;
using TicketService.Features.Tickets.Models;

namespace TicketService.Features.Tickets.IssueTickets;

public sealed class IssueTicketsHandler(
    TicketDbContext dbContext,
    TimeProvider timeProvider)
{
    private const int MaximumQuantity = 10;
    private const string BookingItemTicketIndex =
        "IX_Tickets_BookingItemId_TicketNumberInBookingItem";

    public async Task<IssueTicketsResult> HandleAsync(
        BookingConfirmedMessage message,
        CancellationToken cancellationToken = default)
    {
        if (message is null)
            throw new TicketIssueValidationException(["A booking confirmation is required."]);

        Validate(message);

        var existingTickets = await dbContext.Tickets
            .AsNoTracking()
            .Where(ticket => ticket.BookingItemId == message.BookingItemId)
            .OrderBy(ticket => ticket.TicketNumberInBookingItem)
            .ToArrayAsync(cancellationToken);

        if (existingTickets.Length > 0)
            return ExistingOrThrow(existingTickets, message);

        var issuedAt = timeProvider.GetUtcNow();
        var tickets = Enumerable.Range(1, message.Quantity)
            .Select(number => new Ticket
            {
                TicketId = Guid.NewGuid(),
                BookingId = message.BookingId,
                BookingItemId = message.BookingItemId,
                TicketNumberInBookingItem = number,
                EventId = message.EventId,
                EventName = message.EventName.Trim(),
                EventStartsAt = message.EventStartsAtUtc.ToUniversalTime(),
                VenueName = message.VenueName.Trim(),
                SectionName = message.SectionName.Trim(),
                UnitPrice = message.UnitPrice,
                Currency = message.Currency,
                Status = TicketStatus.Valid,
                IssuedAt = issuedAt
            })
            .ToArray();

        dbContext.Tickets.AddRange(tickets);
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
            return new IssueTicketsResult(tickets, AlreadyIssued: false);
        }
        catch (DbUpdateException exception) when (IsBookingItemTicketConflict(exception))
        {
            dbContext.ChangeTracker.Clear();
            var concurrentlyIssuedTickets = await dbContext.Tickets
                .AsNoTracking()
                .Where(ticket => ticket.BookingItemId == message.BookingItemId)
                .OrderBy(ticket => ticket.TicketNumberInBookingItem)
                .ToArrayAsync(cancellationToken);

            return ExistingOrThrow(concurrentlyIssuedTickets, message);
        }
    }

    private static void Validate(BookingConfirmedMessage message)
    {
        var errors = new List<string>();
        if (message.BookingId == Guid.Empty)
            errors.Add("BookingId is required.");
        if (message.BookingItemId == Guid.Empty)
            errors.Add("BookingItemId is required.");
        if (message.EventId == Guid.Empty)
            errors.Add("EventId is required.");
        if (message.Quantity is < 1 or > MaximumQuantity)
            errors.Add($"Quantity must be between 1 and {MaximumQuantity}.");
        if (string.IsNullOrWhiteSpace(message.EventName) || message.EventName.Trim().Length > 200)
            errors.Add("EventName is required and must be at most 200 characters.");
        if (string.IsNullOrWhiteSpace(message.VenueName) || message.VenueName.Trim().Length > 200)
            errors.Add("VenueName is required and must be at most 200 characters.");
        if (string.IsNullOrWhiteSpace(message.SectionName) || message.SectionName.Trim().Length > 100)
            errors.Add("SectionName is required and must be at most 100 characters.");
        if (message.UnitPrice is < 0 or > 9999999999.99m ||
            decimal.Round(message.UnitPrice, 2) != message.UnitPrice)
            errors.Add("UnitPrice must be non-negative and have at most two decimal places.");
        if (message.Currency is not { Length: 3 } ||
            message.Currency.Any(character => character is < 'A' or > 'Z'))
            errors.Add("Currency must be a three-letter uppercase currency code.");
        if (message.EventStartsAtUtc == default)
            errors.Add("EventStartsAtUtc is required.");

        if (errors.Count > 0)
            throw new TicketIssueValidationException(errors);
    }

    private static IssueTicketsResult ExistingOrThrow(
        Ticket[] existingTickets,
        BookingConfirmedMessage message)
    {
        var expectedStart = message.EventStartsAtUtc.ToUniversalTime();
        var isSameIssue = existingTickets.Length == message.Quantity &&
            existingTickets.Select(ticket => ticket.TicketNumberInBookingItem)
                .SequenceEqual(Enumerable.Range(1, message.Quantity)) &&
            existingTickets.All(ticket =>
                ticket.BookingId == message.BookingId &&
                ticket.EventId == message.EventId &&
                ticket.EventName == message.EventName.Trim() &&
                ticket.EventStartsAt == expectedStart &&
                ticket.VenueName == message.VenueName.Trim() &&
                ticket.SectionName == message.SectionName.Trim() &&
                ticket.UnitPrice == message.UnitPrice &&
                ticket.Currency == message.Currency);

        if (!isSameIssue)
            throw new TicketIssueConflictException(
                "Tickets already exist for this booking item with different confirmation data.");

        return new IssueTicketsResult(existingTickets, AlreadyIssued: true);
    }

    private static bool IsBookingItemTicketConflict(DbUpdateException exception) =>
        exception.GetBaseException() is PostgresException
        {
            SqlState: PostgresErrorCodes.UniqueViolation,
            ConstraintName: BookingItemTicketIndex
        };
}

public sealed record IssueTicketsResult(Ticket[] Tickets, bool AlreadyIssued);

public sealed class TicketIssueValidationException(IReadOnlyList<string> errors)
    : Exception(string.Join(" ", errors))
{
    public IReadOnlyList<string> Errors { get; } = errors;
}

public sealed class TicketIssueConflictException(string message) : Exception(message);
