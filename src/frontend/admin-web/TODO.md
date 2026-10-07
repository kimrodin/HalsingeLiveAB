Uppgift: ta bort evenemang

Det här är oberoende av backends svar om sektioner, så du kan göra det direkt.

TODO 1: API-funktion i api/catalog.ts
 Skriv deleteEvent(id: string): Promise<void>
Följ mönstret från updateEvent: delay, hitta indexet, kasta Error om det inte finns.
Hint: mockEvents är en const-array, så du kan inte tilldela om den. Använd splice istället för filter.

TODO 2: Mutation i EventsPage.tsx
 Lägg till en useMutation med mutationFn: deleteEvent
 I onSuccess: invalidera ['events'] så att listan hämtas om

Hint: du behöver useQueryClient(), precis som i formuläret.


TODO 3: Knapp i tabellraden
 Lägg till en "Ta bort"-knapp bredvid "Redigera"
 Anropa mutation.mutate(event.id) när man klickar

Hint: MUI-knappen har color="error".


TODO 4: Bekräftelsedialog

Radering utan bekräftelse är en klassisk olycka, särskilt i ett adminverktyg.

 Spara vilket evenemang som ska tas bort i useState<EventDetails | null>(null)
 Knappen sätter bara den staten. Själva raderingen sker först i dialogen.
 Rendera MUI:s Dialog med open={eventToDelete !== null}, med knapparna Avbryt och Ta bort
 Stäng dialogen när mutationen lyckats

Hint: komponenterna du behöver är Dialog, DialogTitle, DialogContent, DialogActions.


TODO 5: Felhantering
 Visa en Alert om mutation.isError
 Stäng av Ta bort-knappen i dialogen medan mutation.isPending
Tänk på (att diskutera med backend)
Ska man få ta bort ett evenemang som har sålda biljetter? Troligen inte. Det är mer rimligt att sätta status cancelled. Ta med frågan, och lägg gärna en kommentar i koden där den hör hemma.
Klart när
Du kan ta bort ett utkast, listan uppdateras, och Avbryt i dialogen ändrar ingenting.
npm run build går igenom utan typfel.

Efter det är nästa steg priskategorier. De behöver besked om hur sektioner modelleras, så kolla om backend svarat innan vi börjar på dem.