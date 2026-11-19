import { Alert, Stack, Typography } from '@mui/material'

export default function PrivacyPolicyPage() {
  return (
    <Stack spacing={2}>
      <Typography variant="h1" sx={{ fontSize: { xs: '2rem', sm: '2.5rem' } }}>
        Integritetspolicy
      </Typography>

      <Alert severity="info">
        Hälsinge Live är ett testprojekt. Använd inte riktiga personuppgifter.
      </Alert>

      <Typography>
        Policyn beskriver hur simuleringen hanterar bokningar, betalningsflöden och biljetter.
      </Typography>

      <Typography variant="h2" sx={{ fontSize: '1.35rem' }}>Uppgifter vi behandlar</Typography>
      <Typography>
        Bokningen innehåller namn och e-post. Systemet skapar även boknings-ID, bokningshistorik,
        biljett- och betalningsreferenser samt tekniska meddelandeidentifierare. En gästtoken
        sparas i webbläsarens sessionslagring för att visa och hantera bokningen.
      </Typography>

      <Typography variant="h2" sx={{ fontSize: '1.35rem' }}>Lagring och gallring</Typography>
      <Typography>
        Simuleringen använder en gemensam tvåårsgräns. När en kundgrupp når gränsen tas namn
        och e-post bort från alla bokningar med samma e-postadress. Boknings-, betalnings- och
        biljettuppgifter kan finnas kvar med en gemensam koppling. Det är pseudonymisering,
        inte anonymisering, och gästtoken kan fortsätta fungera.
      </Typography>
      <Typography>
        Alla tekniska lager har inte automatisk tvåårig gallring. Exempelvis kan felköer,
        loggar och säkerhetskopior innehålla bokningsreferenser. Detta är en simulering,
        inte en färdig lösning för verkliga kunduppgifter.
      </Typography>

      <Typography variant="h2" sx={{ fontSize: '1.35rem' }}>Dina rättigheter</Typography>
      <Typography>
        I simuleringen kan en administratör hämta en uppgiftssammanställning eller
        pseudonymisera bokningens direktidentifierare.
      </Typography>
    </Stack>
  )
}
