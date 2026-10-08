namespace EventCatalog.Features.Venues.GetVenue;

public class GetVenueHandler
{
    // Returnerar ett felmeddelande vid ogiltig indata, annars null.
    public void ValidateRequest(GetVenueRequest request)
    {
        
    }

    // TODO: Anropa ValidateRequest först i HandleAsync och avbryt vid valideringsfel.
    public async Task<GetVenueResponse?> HandleAsync(GetVenueRequest request, CancellationToken cancellationToken)
    {
        ValidateRequest(request);
        return new GetVenueResponse();
    }
    // TODO: Låt HandleAsync returnera ett resultat som skiljer på ogiltig indata,
    // saknad lokal och en hittad lokal, så att endpointen kan välja HTTP-status.
    // TODO: Hämta lokalen från katalogens databas med VenueId och CancellationToken.
    // TODO: Läs även sektioner och stolar för numrerad placering och använd läsning utan tracking om EF Core används.
    // TODO: Returnera ett tydligt inte-hittad-resultat om lokalen saknas.
    // TODO: Mappa lokal och platskarta till GetVenueResponse; sortera sektioner, rader och stolar konsekvent.
    // TODO: Platskartan beskriver fysiska stolar. Tillgänglighet och sålda platser per evenemang hämtas från bokningstjänsten.
    // TODO: Verifiera hämtning av befintlig och saknad lokal samt båda placeringstyperna.
}
