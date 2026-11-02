namespace PaymentSimulator.Features.Payments.Simulation;

// Simulation choices are separate from actual payment statuses.
public enum SimulationOutcome
{
    Succeeded = 1,
    Failed = 2,
    TimedOut = 3,
    Random = 4
}
