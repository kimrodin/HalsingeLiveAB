using Booking.Features.Tickets;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;

namespace Booking.UnitTests;

public sealed class AdminTicketsControllerTests
{
    [Fact]
    public async Task CheckInTicket_ReturnsTicketServiceResult()
    {
        var ticketId = Guid.NewGuid();
        var expected = new TicketServiceStatusChangeResponse(ticketId, "CheckedIn", DateTimeOffset.UtcNow);
        var controller = CreateController(new StubTicketServiceClient
        {
            CheckInResult = expected
        });

        var result = await controller.CheckInTicket(ticketId, default, Guid.NewGuid());

        var response = Assert.IsType<OkObjectResult>(result);
        Assert.Same(expected, response.Value);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task TicketActions_MapTicketServiceConflictToConflict(bool checkIn)
    {
        var controller = CreateController(new StubTicketServiceClient
        {
            Exception = new HttpRequestException(
                "Ticket is not valid for this operation.",
                inner: null,
                System.Net.HttpStatusCode.Conflict)
        });

        var result = checkIn
            ? await controller.CheckInTicket(Guid.NewGuid(), default, Guid.NewGuid())
            : await controller.CancelTicket(Guid.NewGuid(), default);

        Assert.IsType<ConflictObjectResult>(result);
    }

    [Fact]
    public void Controller_RequiresAdminPolicy()
    {
        var authorize = Assert.Single(typeof(AdminTicketsController)
            .GetCustomAttributes(typeof(AuthorizeAttribute), inherit: true)
            .Cast<AuthorizeAttribute>());

        Assert.Equal("AdminOnly", authorize.Policy);
    }

    private static AdminTicketsController CreateController(StubTicketServiceClient client) =>
        new(client, NullLogger<AdminTicketsController>.Instance);

    private sealed class StubTicketServiceClient : ITicketServiceClient
    {
        public TicketServiceStatusChangeResponse CheckInResult { get; init; } =
            new(Guid.NewGuid(), "CheckedIn", DateTimeOffset.UtcNow);
        public HttpRequestException? Exception { get; init; }

        public Task IssueTicketsAsync(
            BookingConfirmedMessage message,
            CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task<IReadOnlyList<TicketServiceTicketResponse>> GetTicketsForBookingAsync(
            Guid bookingId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<TicketServiceTicketResponse>>([]);

        public Task<byte[]> GetTicketPdfAsync(
            Guid bookingId,
            Guid ticketId,
            bool download,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(Array.Empty<byte>());

        public Task<TicketServiceStatusChangeResponse> CheckInTicketAsync(
            Guid ticketId,
            Guid eventId,
            CancellationToken cancellationToken = default) =>
            ChangeStatusAsync(CheckInResult);

        public Task<TicketServiceStatusChangeResponse> CancelTicketAsync(
            Guid ticketId,
            CancellationToken cancellationToken = default) =>
            ChangeStatusAsync(new TicketServiceStatusChangeResponse(ticketId, "Cancelled", DateTimeOffset.UtcNow));

        private Task<TicketServiceStatusChangeResponse> ChangeStatusAsync(
            TicketServiceStatusChangeResponse response) =>
            Exception is null ? Task.FromResult(response) : Task.FromException<TicketServiceStatusChangeResponse>(Exception);
    }
}
