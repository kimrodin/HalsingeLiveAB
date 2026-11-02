using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using PaymentSimulator.Data;
using PaymentSimulator.Features.Payments.Models;
using PaymentSimulator.Features.Payments.ProcessPayment;
using PaymentSimulator.Features.Payments.Simulation;

namespace PaymentSimulator.UnitTests;

public sealed class SimulationSettingsTests
{
    [Theory]
    [InlineData((SimulationOutcome)0, 0, 5000)]
    [InlineData((SimulationOutcome)99, 0, 5000)]
    [InlineData(SimulationOutcome.Failed, -1, 5000)]
    [InlineData(SimulationOutcome.Failed, 0, 0)]
    [InlineData(SimulationOutcome.Failed, 0, 30001)]
    public async Task Invalid_settings_are_rejected_before_database_access(SimulationOutcome outcome, int delay, int timeout)
    {
        using var db = new PaymentDbContext(new DbContextOptionsBuilder<PaymentDbContext>().Options);
        var service = new SimulationSettingsService(db, new ConfigurationBuilder().Build());
        await Assert.ThrowsAsync<ArgumentException>(() => service.Update(new(outcome, delay, timeout), default));
    }

    [Fact]
    public async Task Invalid_update_returns_bad_request()
    {
        var configuration = new ConfigurationBuilder().Build();
        using var db = new PaymentDbContext(new DbContextOptionsBuilder<PaymentDbContext>().Options);
        var controller = new SimulationSettingsController(new(db, configuration));
        var result = await controller.Update(new((SimulationOutcome)0, 0, 5000), default);
        Assert.IsType<BadRequestObjectResult>(result.Result);
    }

    [Theory]
    [InlineData("{}")]
    [InlineData("{\"outcome\":\"Failed\",\"timeoutMs\":5000}")]
    [InlineData("{\"outcome\":\"Failed\",\"delayMs\":0}")]
    [InlineData("{\"delayMs\":0,\"timeoutMs\":5000}")]
    public void Missing_fields_are_rejected(string json)
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        options.Converters.Add(new JsonStringEnumConverter());
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<PaymentSimulationSettings>(json, options));
    }
}
