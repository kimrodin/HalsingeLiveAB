namespace EventCatalog.Features.Venues.UpdateVenue;

public class UpdateVenueHandler
{
    // TODO: Hämta lokalen via VenueId; returnera inte-hittad-resultat om den saknas.
    // TODO: Validera ändrade uppgifter med samma regler som vid skapande.
    // TODO: Kontrollera kopplade evenemang innan kapacitet eller platskarta ändras; fastställ regeln med produktägaren.
    // TODO: Bevara stolarnas id:n så att befintliga evenemang och bokningar inte förlorar sina referenser.
    // TODO: Spara tillåtna ändringar atomärt och skicka vidare CancellationToken.
    // TODO: Om samtidighetskontroll används: upptäck ändringar sedan läsningen och returnera konflikt i stället för att skriva över.
    // TODO: Verifiera giltig uppdatering, ogiltig indata, saknad lokal och skydd av platskartor som används.
}
