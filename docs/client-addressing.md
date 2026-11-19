# Klientadress genom webbproxy och gateway

## Tillit vid varje led

- Nginx och Vite är yttersta proxy i den medföljande konfigurationen. De ersätter
  `X-Forwarded-For` med TCP-anslutningens adress och `X-Forwarded-Proto` med
  anslutningens protokoll. Kundens `X-Real-IP` och `Forwarded` tas bort.
  Vite gör detta även vid WebSocket-uppgradering i adminappen.
- Gateway accepterar forwarding-information endast från adresserna i
  `ClientAddressing:TrustedProxies`, normalt de två webbproxyerna.
- YARP ersätter forwarding-informationen med en ny uppsättning utifrån
  gatewayens verifierade anslutningskontext (`X-Forwarded: Set`).
- Booking accepterar motsvarande information endast från gatewayens adress.

Varje .NET-led behandlar högst **ett** proxyhopp. `X-Forwarded-For` och
`X-Forwarded-Proto` måste ha lika många värden. Okända avsändare och ogiltiga
adressvärden får inte ersätta anslutningsadressen. Efter behandlingen rensas
forwardingheaders och `X-Original-*` så att annan kod inte använder kvarvarande
eller alternativa adresspåståenden.

Ett direkt anrop till gateway eller Booking kan därför inte välja klientadress
med en egen header: den verkliga anslutningsadressen används. En tom lista av
betrodda proxyer stänger av headerbehandling. Produktionskonfigurationen har
tomma listor som standard. Endast individuella IP-adresser accepteras i listorna;
DNS-namn och nätverksintervall accepteras inte.

Den generella flaggan `ASPNETCORE_FORWARDEDHEADERS_ENABLED` åsidosätts av
adresskomponenten. Den explicita adresshanteringen ska köras en gång per led;
automatisk extra middleware får inte konsumera ytterligare ett proxyhopp.

## Compose och lokal utveckling

Compose sätter fasta adresser för proxyerna och använder samma värden i
tillitslistorna:

| Variabel | Standard | Användning |
| --- | --- | --- |
| `PROXY_NETWORK_SUBNET` | `172.30.151.0/24` | Projektets Compose-nätverk. |
| `PROXY_NETWORK_DYNAMIC_RANGE` | `172.30.151.128/25` | Pool för övriga containrar. |
| `GATEWAY_PROXY_IP` | `172.30.151.2` | Gatewayens adress; betrodd av Booking. |
| `ADMIN_PROXY_IP` | `172.30.151.3` | Adminproxyns adress; betrodd av gateway. |
| `CUSTOMER_PROXY_IP` | `172.30.151.4` | Kundproxyns adress; betrodd av gateway. |

Variablerna är valfria i `.env`. Vid konflikt med ett befintligt nätverk måste
samtliga fem värden väljas tillsammans. Proxyadresserna ska vara olika, ligga
i subnätet och utanför den dynamiska poolen. Adresserna behålls vid omstart.
Övriga containrar i nätverket är inte betrodda proxyer.

När en befintlig installation får denna nätverkskonfiguration måste det gamla
Compose-nätverket återskapas. Stoppa systemet vid ett lämpligt tillfälle:

```sh
docker compose down
docker compose up -d --build
```

Kommandona behåller datavolymerna. Webbapparnas proxyadress ska fortsatt vara
`http://api-gateway:8080`. Både Nginx- och Vite-läget använder samma fasta
containeradresser. Gatewayens båda Development-poster skrivs över av Compose;
Booking får en explicit gatewaypost. Loopback lämnas inte som extra betrodd
proxy i containrarna.

Vid `dotnet run` med de medföljande utvecklingsprofilerna litar gateway och Booking på
`127.0.0.1` och `::1`, inklusive IPv4-mappad loopback. Detta förutsätter att
lokala processer är betrodda. Gateway använder `appsettings.Development.json`
och Booking sin `Properties/launchSettings.json`. Vid körning utan dessa
profiler eller i andra miljöer anges listorna uttryckligen,
exempelvis med `ClientAddressing__TrustedProxies__0`. Vid ändringar behöver
applikationen startas om. Aktivera inte generell tillit till alla proxyer och
lägg inte till okontrollerade adressposter i konfigurationen.

## Normalisering och begränsningar

Normaliseringen körs efter verifieringen i både gateway och Booking:

| Adress | Normaliserad adress |
| --- | --- |
| `192.0.2.1` | `192.0.2.1` |
| `::ffff:192.0.2.1` | `192.0.2.1` |
| `2001:0db8:0000:0000:0000:0000:0000:0001` | `2001:db8::1` |
| `fe80::1%12` | `fe80::1` |

Adresser normaliseras från sina bytes; IPv6-scope-ID är lokal
gränssnittsinformation och ingår inte i klientidentiteten. En saknad adress
förblir saknad och får inte ersättas med kundens header. IPv6 jämförs som hel
adress; ingen automatisk gruppering till exempelvis `/64` sker.

NAT och Docker Desktop kan dölja ursprungsadressen. Systemet kan bara använda
den anslutningsadress yttersta proxyn faktiskt ser. Personer bakom samma NAT
kan därför dela adress, och byte av en verklig IPv6-adress ger en annan
klientadress. Detta är inte ett skydd mot distribuerade klienter eller adressbyte.

En extra tunnel, CDN eller lastbalanserare framför webbproxyn ingår inte som
betrott led som standard. Den dessförinnan verifierade ursprungsadressen måste
konfigureras separat vid den yttersta proxyn, med uttrycklig tillit till just
den avsändaren. Att enbart börja bevara kundens `X-Forwarded-For` är osäkert.
I en sådan miljö ska hela kedjan verifieras igen. Konfigurationen förutsätter
att angriparen inte kan styra Docker-nätverket eller en betrodd proxy.

## Verifiering

```sh
dotnet test tests/ClientAddressing.Tests/ClientAddressing.Tests.csproj
node --test tests/proxy-chain/vite.test.mjs
docker compose -p halsingelive-proxy-tests -f tests/proxy-chain/compose.yaml up --build --abort-on-container-exit --exit-code-from client
docker compose -p halsingelive-proxy-tests -f tests/proxy-chain/compose.yaml down
```

Vite-testet kräver `npm ci` i båda webbapparna och Node 22.18 eller senare.
.NET-testerna verifierar normalisering, explicita tillitslistor, saknade och
felaktiga headers, ett-hoppsgränsen samt HTTP genom riktig YARP med gatewayens
levererade ruttkonfiguration. De kräver inga databaser.

Docker-testet använder båda webbapparnas riktiga Nginx-mallar, den riktiga
gatewayimagen och en testserver med samma adressmiddleware som Booking. En
separat klientcontainer provar IPv4 och IPv6 genom hela kedjan, förfalskade
headers och direkta anrop som passerar förbi proxyleden. Testserverns
diagnostikendpoint finns endast i testimagen. Testet använder ett eget
nätverk (`172.30.152.0/24`, `fd00:151:152::/64`), inga värdportar eller
beständiga datavolymer och kräver att Docker stöder IPv6.

Se även [Microsofts proxyguide](https://learn.microsoft.com/en-us/aspnet/core/host-and-deploy/proxy-load-balancer?view=aspnetcore-10.0)
och [YARP:s forwardingtransformer](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/servers/yarp/transforms-request).

Riktiga bokningsanrop, 429 och kvoter genom frontendproxy → gateway → Booking
verifieras separat av [reservationskedjans test](../tests/reservation-chain/README.md).
Proxyernas accessloggar och avgränsningen mot feldiagnostik beskrivs i
[reservationsguiden](reservation-limits.md).
