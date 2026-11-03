namespace TicketService.IntegrationTests;

// These smoke tests run without a database and never change the process environment.
public sealed class TicketIntegrationTheoryAttributeTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public void MissingConnectionString_SkipsWithSetupInstructions(string? connectionString)
    {
        var attribute = new TicketIntegrationTheoryAttribute(connectionString);

        Assert.Equal("Set TICKET_TEST_POSTGRES to run against PostgreSQL.", attribute.Skip);
    }

    [Fact]
    public void ConfiguredConnectionString_DoesNotSkip()
    {
        var attribute = new TicketIntegrationTheoryAttribute("Host=localhost;Database=postgres");

        Assert.Null(attribute.Skip);
    }
}
