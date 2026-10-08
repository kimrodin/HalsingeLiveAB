using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace EventCatalog.Features.Venues.CreateVenue;

[ApiController]
[Route("venues")]
public class CreateVenueEndpoint(CreateVenueHandler handler) : ControllerBase
{
    // För inkoppling i EventCatalogs API:
    // 1. Projektet behöver tillgång till ASP.NET Core via Web SDK eller framework reference.
    // 2. Registrera controllers: builder.Services.AddControllers();
    // 3. Registrera handlern i DI: builder.Services.AddScoped<CreateVenueHandler>();
    // 4. Registrera routes efter builder.Build(): app.MapControllers();
    // Om API-hosten ligger i ett annat assembly kan den behöva:
    // builder.Services.AddControllers().AddApplicationPart(typeof(CreateVenueEndpoint).Assembly);
    // Dessa steg görs i projektfilen/API-hosten, inte i denna endpoint.

    /// <summary>
    /// Tar emot POST /venues, anropar handlern och returnerar HTTP-svaret.
    /// </summary>
    [HttpPost]
    [Consumes("application/json")]
    [ProducesResponseType(StatusCodes.Status201Created, Type = typeof(CreateVenueResponse))]
    [ProducesResponseType(StatusCodes.Status400BadRequest, Type = typeof(ValidationProblemDetails))]
    // TODO: Lägg till [Authorize(Policy = "VenueAdmin")] när policyn finns i API-hosten.
    // TODO: Dokumentera 401 och 403 när behörighetskontrollen kopplas in.
    public async Task<IActionResult> Create(
        [FromBody] CreateVenueRequest request,
        CancellationToken cancellationToken)
    {
        // [ApiController] hanterar bindningsfel och ogiltigt ModelState med 400.
        // Verksamhetsvalidering, exempelvis positiv kapacitet, hör till handlern.
        // ASP.NET Core binder JSON till requesten och injicerar handlern i konstruktorn.
        // Token skickas vidare så att handlerns databasarbete kan avbrytas.
        var response = await handler.HandleAsync(request, cancellationToken);

        // TODO: När handlern returnerar valideringsfel, mappa dem till
        // ValidationProblem med fel per fält, exempelvis via ModelState.AddModelError.
        // Håll verksamhetsreglerna i handlern så att endpointen bara hanterar HTTP.
        // Oväntade fel bör hanteras centralt som 500, inte fångas här som 400.

        // TODO: När response innehåller VenueId och GET-routen finns, ange Location:
        // return Created($"/venues/{response.VenueId}", response);
        // Response är ännu en tom stomme, så ingen resursadress kan anges här.
        return StatusCode(StatusCodes.Status201Created, response);
    }
}
