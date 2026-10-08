namespace EventCatalog.Features.Venues.DeleteVenue;

public class DeleteVenueHandler
{
    // TODO: Hämta lokalen via VenueId och hantera en saknad lokal konsekvent med endpointens 404-svar.
    // TODO: Kontrollera om evenemang refererar till lokalen innan den tas bort.
    // TODO: Fastställ med produktägaren om använda lokaler ska arkiveras i stället för att raderas.
    // TODO: Förhindra att borttagning raderar evenemang eller gör historiska platsreferenser ogiltiga.
    // TODO: Om fysisk borttagning tillåts: hantera tillhörande sektioner och stolar atomärt i katalogens databas.
    // TODO: Låt även databasens referensregler skydda mot borttagning när lokalen används; hantera konflikt vid samtidiga anrop.
    // TODO: Verifiera borttagning av oanvänd lokal, saknad lokal och konflikt när lokalen används.
}
