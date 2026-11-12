# Seq

Starta tjänsterna från repositoryts rotkatalog med
`docker compose --env-file .env -f src/Compose.yaml up -d --build`.
Öppna [http://localhost:5341](http://localhost:5341). Den lokala Seq-instansen
körs utan autentisering. Gör den inte tillgänglig utanför en betrodd
utvecklingsmiljö.

De fem .NET-tjänsterna skickar strukturerade loggar till `http://seq:5341` och
fortsätter även att skriva till sina containrars konsol. Varje logghändelse
innehåller `ServiceName` och `Environment`. Den publicerade porten för
webbgränssnittet är som standard `5341` och kan ändras med `SEQ_PUBLISHED_PORT`.
Konfigurera adressen för logginläsning med `Serilog__SeqServerUrl` (Compose
anger `http://seq:5341`).

Varje lyckad reservation returnerar sitt ID i `X-Correlation-ID`. Gatewayens
och Bookings logghändelser vid avslutade anrop innehåller det som `BookingId`
och `CorrelationId`. Betalnings- och biljettmeddelanden använder samma
boknings-ID som `CorrelationId`.

Följ ett köp genom att skapa en reservation och slutföra `StartPayment`. Sök
sedan efter `CorrelationId = 'booking-guid'`. Om du vill testa ett misslyckat
köp anger du `PAYMENT_SIMULATION_OUTCOME=Failed` i `.env`, återskapar
`payment-simulator` och använder en ny reservation. Detaljerade steg för att
testa köp finns i [betalningsguiden](payments.md).
