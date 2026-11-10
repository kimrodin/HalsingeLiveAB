using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace Booking.Features.Statistics;

[Authorize(Policy = "AdminOnly")]
public sealed class SalesUpdatesHub : Hub { }
