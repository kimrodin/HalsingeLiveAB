using System.Net;
using System.Net.Http.Json;
using Booking.Features.Bookings;
using Booking.Features.EventCatalog;
using Booking.Features.Payments;
using Booking.Features.Tickets;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Booking.IntegrationTests;

// Exercises actual MVC action routing and the HTTP payment adapter against an
// isolated PostgreSQL database/Redis inventory. Only the remote simulator is stubbed.
public sealed class BookingPaymentHttpTests(BookingDatabaseFixture fixture) : IClassFixture<BookingDatabaseFixture>
{
    [BookingIntegrationFact]
    public Task TwoCustomersBookingTheLastPlace_OneGets202AndTheOtherGets409() =>
        AssertConcurrentBookingsAsync(2);

    [BookingIntegrationFact]
    public Task TwoHundredCustomersBookingTheLastPlace_OnlyOneGets202() =>
        AssertConcurrentBookingsAsync(200);

    private async Task AssertConcurrentBookingsAsync(int customers)
    {
        var inventory = await fixture.CreateInventoryAsync(stock: 1);
        using var simulator = new SimulatorHandler(Guid.NewGuid(), inventory.Price);
        var snapshots = new SynchronizedSnapshotProvider(fixture.EventSnapshots, customers);
        await using var host = await StartHostAsync(simulator, snapshots);

        var responses = await Task.WhenAll(Enumerable.Range(0, customers).Select(customer =>
            host.Client.PostAsJsonAsync("/CreateBooking", new CreateBookingRequest(
                inventory.InventoryId, 1, $"Customer {customer}", $"customer{customer}@example.com"))));
        try
        {
            var accepted = Assert.Single(responses, response => response.StatusCode == HttpStatusCode.Accepted);
            var reservation = await accepted.Content.ReadFromJsonAsync<CreateBookingResponse>();
            Assert.NotNull(reservation);
            Assert.NotEqual(Guid.Empty, reservation.BookingId);
            Assert.False(string.IsNullOrWhiteSpace(reservation.GuestAccessToken));
            var rejected = responses.Where(response => response != accepted).ToArray();
            Assert.Equal(customers - 1, rejected.Length);
            foreach (var response in rejected)
            {
                Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
                var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
                Assert.NotNull(problem);
                Assert.Equal(409, problem.Status);
                Assert.Equal("Not enough tickets are available.", problem.Title);
            }

            await using var db = fixture.CreateContext();
            var booking = Assert.Single(await db.Bookings.Include(row => row.Items)
                .Where(row => row.Items.Any(item => item.InventoryId == inventory.InventoryId)).ToListAsync());
            Assert.Equal(reservation.BookingId, booking.BookingId);
            Assert.Equal(BookingStatuses.Reserved, booking.Status);
            Assert.Equal(1, Assert.Single(booking.Items).Quantity);
            Assert.True(GuestAccessToken.Matches(reservation.GuestAccessToken, booking.GuestAccessTokenHash));
            Assert.Equal(0, await fixture.Reservations.GetAvailableQuantityAsync(
                inventory.EventId, inventory.SectionId, inventory.TotalQuantity));
            // The database deducts stock only after payment, not during the temporary hold.
            Assert.Equal(1, (await db.EventInventory.SingleAsync(
                row => row.InventoryId == inventory.InventoryId)).AvailableQuantity);
            Assert.Equal(0, simulator.RequestCalls);
        }
        finally
        {
            foreach (var response in responses)
                response.Dispose();
        }
    }

    [BookingIntegrationFact]
    public async Task GuestTicketEndpoints_RequireTheBookingAccessToken()
    {
        var inventory = await fixture.CreateInventoryAsync();
        var id = await fixture.ReserveAsync(inventory, null!);
        var ticketId = Guid.NewGuid();
        using var simulator = new SimulatorHandler(id, inventory.Price);
        await using var host = await StartHostAsync(simulator);

        using var bookingDenied = await host.Client.GetAsync($"/GetBooking/{id}");
        using var ticketsDenied = await host.Client.GetAsync($"/GetTicketsForBooking/{id}/tickets");
        using var pdfDenied = await host.Client.GetAsync($"/DownloadTicketPdf/{id}/tickets/{ticketId}/pdf");
        Assert.Equal(HttpStatusCode.NotFound, bookingDenied.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, ticketsDenied.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, pdfDenied.StatusCode);

        using var booking = await GetGuestAsync(host.Client, $"/GetBooking/{id}", id);
        using var tickets = await GetGuestAsync(host.Client, $"/GetTicketsForBooking/{id}/tickets", id);
        using var pdf = await GetGuestAsync(
            host.Client, $"/DownloadTicketPdf/{id}/tickets/{ticketId}/pdf", id);
        Assert.Equal(HttpStatusCode.OK, booking.StatusCode);
        Assert.Equal(HttpStatusCode.OK, tickets.StatusCode);
        Assert.Equal(HttpStatusCode.OK, pdf.StatusCode);
        Assert.Equal("application/pdf", pdf.Content.Headers.ContentType!.MediaType);
    }

    [BookingIntegrationFact]
    public async Task ConfirmStartAndCancel_ResolveToDistinctPostActions()
    {
        var inventory = await fixture.CreateInventoryAsync();
        var id = await fixture.ReserveAsync(inventory, null!);
        using var simulator = new SimulatorHandler(id, inventory.Price);
        await using var host = await StartHostAsync(simulator);

        using var denied = await host.Client.PostAsync($"/ConfirmBooking/{id}", null);
        Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);
        Assert.Equal(0, simulator.ProcessCalls);
        using var noToken = await host.Client.PostAsync($"/StartPayment/{id}", null);
        Assert.Equal(HttpStatusCode.NotFound, noToken.StatusCode);
        using var paid = await PostGuestAsync(host.Client, $"/StartPayment/{id}", id);
        Assert.Equal(HttpStatusCode.OK, paid.StatusCode);
        Assert.Equal(BookingStatuses.Confirmed, (await paid.Content.ReadFromJsonAsync<StartPaymentResult>())!.BookingStatus);
        using var repeated = await PostGuestAsync(host.Client, $"/StartPayment/{id}", id);
        Assert.Equal(HttpStatusCode.OK, repeated.StatusCode);
        Assert.Equal(1, simulator.ProcessCalls);

        var other = await fixture.ReserveAsync(inventory, null!);
        using var cancelled = await PostGuestAsync(host.Client, $"/CancelBooking/{other}", other);
        Assert.Equal(HttpStatusCode.OK, cancelled.StatusCode);
        Assert.Equal(BookingStatuses.Cancelled,
            (await cancelled.Content.ReadFromJsonAsync<BookingStatusResponse>())!.Status);
        await using var db = fixture.CreateContext();
        Assert.Equal(9, (await db.EventInventory.SingleAsync(row => row.InventoryId == inventory.InventoryId)).AvailableQuantity);
    }

    [BookingIntegrationFact]
    public async Task FailedAndTimedOutPayments_Return409AndReleaseTheReservation()
    {
        foreach (var outcome in new[] { "Failed", "TimedOut" })
        {
            var inventory = await fixture.CreateInventoryAsync();
            var id = await fixture.ReserveAsync(inventory, null!);
            using var simulator = new SimulatorHandler(id, inventory.Price) { Outcome = outcome };
            await using var host = await StartHostAsync(simulator);

            using var response = await PostGuestAsync(host.Client, $"/StartPayment/{id}", id);

            Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
            var result = (await response.Content.ReadFromJsonAsync<StartPaymentResult>())!;
            Assert.Equal(outcome, result.PaymentStatus);
            Assert.Equal(BookingStatuses.Cancelled, result.BookingStatus);
            Assert.Equal(10, await fixture.Reservations.GetAvailableQuantityAsync(inventory.EventId, inventory.SectionId, 10));
            await using var db = fixture.CreateContext();
            var booking = await db.Bookings.SingleAsync(row => row.BookingId == id);
            Assert.True(booking.PaymentSettled);
            Assert.Equal(10, (await db.EventInventory.SingleAsync(row => row.InventoryId == inventory.InventoryId)).AvailableQuantity);
        }
    }

    [BookingIntegrationFact]
    public async Task Simulator409_Returns502InvalidWithoutCancellingOrSettlingTheBooking()
    {
        var inventory = await fixture.CreateInventoryAsync();
        var id = await fixture.ReserveAsync(inventory, null!);
        using var simulator = new SimulatorHandler(id, inventory.Price) { ProcessStatus = HttpStatusCode.Conflict };
        await using var host = await StartHostAsync(simulator);

        using var response = await PostGuestAsync(host.Client, $"/StartPayment/{id}", id);

        Assert.Equal(HttpStatusCode.BadGateway, response.StatusCode);
        Assert.Equal("Invalid", (await response.Content.ReadFromJsonAsync<StartPaymentResult>())!.PaymentStatus);
        await AssertUnresolvedAsync(id);
    }

    [BookingIntegrationFact]
    public async Task Simulator502_Returns202UnknownAndRetainsTheAttemptForRecovery()
    {
        var inventory = await fixture.CreateInventoryAsync();
        var id = await fixture.ReserveAsync(inventory, null!);
        using var simulator = new SimulatorHandler(id, inventory.Price) { ProcessStatus = HttpStatusCode.BadGateway };
        await using var host = await StartHostAsync(simulator);

        using var response = await PostGuestAsync(host.Client, $"/StartPayment/{id}", id);

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        Assert.Equal("Unknown", (await response.Content.ReadFromJsonAsync<StartPaymentResult>())!.PaymentStatus);
        await AssertUnresolvedAsync(id);

        // A subsequent status lookup can recover a committed payment without another POST.
        simulator.StoredPayment = simulator.Payment;
        using var recovered = await PostGuestAsync(host.Client, $"/StartPayment/{id}", id);
        Assert.Equal(HttpStatusCode.OK, recovered.StatusCode);
        Assert.Equal(1, simulator.ProcessCalls);
    }

    [BookingIntegrationFact]
    public async Task MissingBooking_Returns404WithoutCallingTheSimulator()
    {
        using var simulator = new SimulatorHandler(Guid.NewGuid(), 100m);
        await using var host = await StartHostAsync(simulator);

        using var response = await host.Client.PostAsync($"/StartPayment/{Guid.NewGuid()}", null);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal(0, simulator.RequestCalls);
    }

    [BookingIntegrationFact]
    public async Task MalformedPaymentResponse_Returns502WithoutConfirmingOrSettling()
    {
        var inventory = await fixture.CreateInventoryAsync();
        var id = await fixture.ReserveAsync(inventory, null!);
        using var simulator = new SimulatorHandler(id, inventory.Price) { MalformedResponse = true };
        await using var host = await StartHostAsync(simulator);

        using var response = await PostGuestAsync(host.Client, $"/StartPayment/{id}", id);

        Assert.Equal(HttpStatusCode.BadGateway, response.StatusCode);
        Assert.Equal("Invalid", (await response.Content.ReadFromJsonAsync<StartPaymentResult>())!.PaymentStatus);
        await AssertUnresolvedAsync(id);
    }

    [BookingIntegrationFact]
    public async Task MissingRefund_Returns502AndKeepsCompensationUnresolved()
    {
        var inventory = await fixture.CreateInventoryAsync();
        var id = await fixture.ReserveAsync(inventory, null!);
        using var simulator = new SimulatorHandler(id, inventory.Price)
        {
            RefundStatus = HttpStatusCode.NotFound,
            BeforeProcess = async () =>
            {
                await using var db = fixture.CreateContext();
                await db.Bookings.Where(row => row.BookingId == id).ExecuteUpdateAsync(update =>
                    update.SetProperty(row => row.ExpiresAt, DateTime.UtcNow.AddSeconds(-1)));
            }
        };
        await using var host = await StartHostAsync(simulator);

        using var response = await PostGuestAsync(host.Client, $"/StartPayment/{id}", id);

        Assert.Equal(HttpStatusCode.BadGateway, response.StatusCode);
        Assert.Equal("Invalid", (await response.Content.ReadFromJsonAsync<StartPaymentResult>())!.PaymentStatus);
        await AssertUnresolvedAsync(id);
    }

    private async Task AssertUnresolvedAsync(Guid id)
    {
        await using var db = fixture.CreateContext();
        var booking = await db.Bookings.SingleAsync(row => row.BookingId == id);
        Assert.Equal(BookingStatuses.Reserved, booking.Status);
        Assert.True(booking.PaymentRequested);
        Assert.False(booking.PaymentSettled);
    }

    private async Task<HttpResponseMessage> PostGuestAsync(
        HttpClient client,
        string path,
        Guid bookingId)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, path);
        request.Headers.Add("X-Guest-Access-Token", fixture.GetGuestAccessToken(bookingId));
        return await client.SendAsync(request);
    }

    private async Task<HttpResponseMessage> GetGuestAsync(
        HttpClient client,
        string path,
        Guid bookingId)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, path);
        request.Headers.Add("X-Guest-Access-Token", fixture.GetGuestAccessToken(bookingId));
        return await client.SendAsync(request);
    }

    private async Task<HttpTestHost> StartHostAsync(
        HttpMessageHandler simulator,
        IEventSnapshotProvider? snapshots = null)
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = "Development" });
        builder.Logging.ClearProviders();
        builder.WebHost.ConfigureKestrel(options => options.Listen(IPAddress.Loopback, 0));
        builder.Services.AddControllers().AddApplicationPart(typeof(BookingsController).Assembly);
        builder.Services.AddScoped(_ => fixture.CreateContext());
        builder.Services.AddSingleton(fixture.Reservations);
        builder.Services.AddSingleton(snapshots ?? fixture.EventSnapshots);
        builder.Services.AddScoped<BookingService>();
        builder.Services.AddSingleton<ITicketServiceClient, StubTicketServiceClient>();
        builder.Services.AddHttpClient<IPaymentGateway, HttpPaymentGateway>(client =>
            client.BaseAddress = new Uri("http://payment-simulator/"))
            .ConfigurePrimaryHttpMessageHandler(() => simulator);
        var app = builder.Build();
        app.MapControllers();
        try
        {
            await app.StartAsync();
            var address = app.Services.GetRequiredService<IServer>().Features
                .Get<IServerAddressesFeature>()!.Addresses.Single();
            return new HttpTestHost(app, new HttpClient { BaseAddress = new Uri(address) });
        }
        catch
        {
            await app.DisposeAsync();
            throw;
        }
    }

    private sealed class SynchronizedSnapshotProvider(
        IEventSnapshotProvider inner,
        int customers) : IEventSnapshotProvider
    {
        private readonly ConcurrentRequestGate gate = new(customers);

        public async Task<EventSnapshot> GetSnapshotAsync(
            Guid eventId, Guid sectionId, CancellationToken cancellationToken = default)
        {
            var snapshot = await inner.GetSnapshotAsync(eventId, sectionId, cancellationToken);
            // Every request has read the same database stock before racing for the Redis hold.
            await gate.ArriveAsync(cancellationToken);
            return snapshot;
        }
    }

    private sealed class HttpTestHost(WebApplication app, HttpClient client) : IAsyncDisposable
    {
        public HttpClient Client => client;
        public async ValueTask DisposeAsync()
        {
            Client.Dispose();
            await app.DisposeAsync();
        }
    }

    private sealed class StubTicketServiceClient : ITicketServiceClient
    {
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
    }

    private sealed class SimulatorHandler(Guid bookingId, decimal amount) : HttpMessageHandler
    {
        public string Outcome { get; init; } = "Succeeded";
        public HttpStatusCode ProcessStatus { get; init; } = HttpStatusCode.OK;
        public HttpStatusCode RefundStatus { get; init; } = HttpStatusCode.OK;
        public bool MalformedResponse { get; init; }
        public Func<Task>? BeforeProcess { get; init; }
        public int RequestCalls { get; private set; }
        public int ProcessCalls { get; private set; }
        public PaymentResult? StoredPayment { get; set; }
        public PaymentResult Payment { get; } = new(Guid.NewGuid(), bookingId, amount, "SEK", "Succeeded",
            DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, null);

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            RequestCalls++;
            if (request.Method == HttpMethod.Get)
                return StoredPayment is null ? new(HttpStatusCode.NotFound) : Json(StoredPayment);
            if (request.RequestUri!.AbsolutePath.EndsWith("/refund"))
                return RefundStatus == HttpStatusCode.OK
                    ? Json(Payment with { RefundedAt = DateTimeOffset.UtcNow }) : new(RefundStatus);

            Assert.Equal("/api/payments", request.RequestUri.AbsolutePath);
            ProcessCalls++;
            if (BeforeProcess is not null)
                await BeforeProcess();
            if (ProcessStatus != HttpStatusCode.OK)
                return new(ProcessStatus);
            if (MalformedResponse)
                return new(HttpStatusCode.OK) { Content = new StringContent("invalid JSON") };
            StoredPayment = Payment with { Status = Outcome };
            return Json(StoredPayment);
        }

        private static HttpResponseMessage Json(PaymentResult payment) =>
            new(HttpStatusCode.OK) { Content = JsonContent.Create(payment) };
    }
}
