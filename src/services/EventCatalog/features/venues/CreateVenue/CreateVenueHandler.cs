namespace EventCatalog.Features.Venues.CreateVenue;

public class CreateVenueHandler() //(EventCatalogDbContext dbContext)
{
    // TODO: Ta emot request, katalogens databasåtkomst och CancellationToken via en asynkron metod.
    public async Task<CreateVenueResponse> HandleAsync(CreateVenueRequest request, CancellationToken cancellationToken)
    {
        return new CreateVenueResponse();
    }
    // TODO: Validera att namn inte är tomt, kapacitet är positiv och SeatingType är ett tillåtet värde.
    private static void ValidateRequest(CreateVenueRequest request)
    {

    }
    // TODO: Bestäm med produktägaren vilka adressuppgifter som måste finnas, även för festivalområden.
    private static void ValidateAddress(CreateVenueRequest request)
    {

    }
    // TODO: För numrerad placering: kontrollera sektioner, unika stolnummer per rad och att antal stolar stämmer med kapaciteten.
    private static void ValidateSeating(CreateVenueRequest request)
    {

    }
    // TODO: För fri placering: använd kapaciteten som gräns utan att skapa individuella stolar.
    private static void ValidateCapacity(CreateVenueRequest request)
    {

    }
    // // TODO: Skapa id:n och spara lokal, sektioner och stolar atomärt i katalogens databas.
    // public async Task<Venue> CreateVenueAsync(CreateVenueRequest request, CancellationToken cancellationToken)
    // {
    //     return new Venue();
    // }
    // // TODO: Mappa resultatet till CreateVenueResponse; returnera ett tydligt valideringsfel vid ogiltig indata.
    // public CreateVenueResponse MapToResponse(Venue venue)
    // {
    //     return new CreateVenueResponse();
    // }
    // // TODO: Verifiera reglerna med tester för giltig lokal, ogiltig kapacitet och dubbla stolnummer.
    // public void TestValidationRules()
    // {

    // }
}
