# CI med GitHub Actions

*Continuous Integration* flödet i `.github/workflows/ci.yml` kontrollerar automatiskt att kodändringar går att bygga och att projektets kvalitetskontroller passerar.

Flödet startar när en commit pushas till `main` eller när en pull request skapas mot `main`. Det körs på GitHubs Ubuntu-miljö. Om en ny körning startar för samma branch avbryts en äldre pågående körning.

Flödet innehåller tre jobb:

- **.NET:** installerar .NET 10 SDK, återställer NuGet-paket och bygger `HalsingeLiveAB.sln` i Release-läge. Därefter körs testprojekten i lösningen. PostgreSQL och Redis startas som tjänster för integrationstesterna. Anslutningsuppgifterna gäller endast den tillfälliga CI-miljön.
- **Frontend:** körs separat för `admin-web` och `customer-web`. För varje app installeras de låsta npm-beroendena, ESLint kontrollerar koden och Vite bygger appen för produktion.
- **Containrar:** kontrollerar att `Compose.yaml` tillsammans med `Compose.dev.yaml` ger en giltig Docker Compose-konfiguration. Därefter byggs Docker-avbildningar för Booking, EventCatalog, PaymentSimulator och TicketService.

Containrarbetet startar först när både .NET- och frontendjobben har lyckats. .NET- och frontendjobben kan köras parallellt.

Om ett steg misslyckas markeras jobb och körning som misslyckade. Loggarna i GitHub Actions visar vilket kommando som fallerade och dess felmeddelande. En grön körning betyder att alla tre jobb har lyckats.

Flödet har endast läsbehörighet till repot. Det publicerar inga avbildningar och driftsätter inte tjänsterna.
