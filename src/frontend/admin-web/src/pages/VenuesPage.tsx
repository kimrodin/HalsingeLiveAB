import { Link as RouterLink } from 'react-router';
import {
  Alert, Box, Button, CircularProgress, Dialog, DialogActions, DialogContent, DialogTitle, Paper, Stack, Table, TableBody, TableCell,
  TableContainer, TableHead, TableRow, Typography,
} from '@mui/material';
import type { SectionSeatingType } from '../api/catalog';
import type { Venue } from '../api/catalog';
import { getVenues, deleteVenue } from '../api/catalog';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { useState } from 'react';

const seatingTypeLabel: Record<SectionSeatingType, string> = {
  standing: 'Ståplats',
  seated: 'Sittplats',
};

export default function VenuesPage() {

    const queryClient = useQueryClient();
  
    const [venueToDelete, setVenueToDelete] = useState<Venue | null>(null);
    const [dialogOpen, setDialogOpen] = useState(false);
  
    const mutation = useMutation({
      mutationFn: deleteVenue,
      onSuccess: async () => {
        await queryClient.invalidateQueries({ queryKey: ['venues'] });
        setDialogOpen(false);
      }
    });

  const { data: venues, isPending, isError } = useQuery({
    queryKey: ['venues'],
    queryFn: getVenues,
  });

  return (
    <>
      <Box sx={{ display: 'flex', justifyContent: 'space-between', mb: 2 }}>
        <Typography variant="h4">Scener</Typography>
        <Button variant="contained" component={RouterLink} to="/venues/new">
          Lägg till scen
        </Button>
      </Box>

      {isPending && <CircularProgress />}
      {isError && <Alert severity="error">Kunde inte hämta scener.</Alert>}
      {venues && venues.length === 0 && (
        <Alert severity="info">Det finns inga scener ännu. Lägg till en för att komma igång.</Alert>
      )}

      {venues && venues.length > 0 && (
        <TableContainer component={Paper}>
          <Table>
            <TableHead>
              <TableRow>
                <TableCell>Namn</TableCell>
                <TableCell>Adress</TableCell>
                <TableCell align="right">Kapacitet</TableCell>
                <TableCell>Sektioner</TableCell>
                <TableCell />
              </TableRow>
            </TableHead>
            <TableBody>
              {venues.map((venue) => (
                <TableRow key={venue.id} hover>
                  <TableCell>{venue.name}</TableCell>
                  <TableCell>{venue.address ?? '–'}</TableCell>
                  <TableCell align="right">
                    {venue.capacity === undefined ? '–' : venue.capacity.toLocaleString('sv-SE')}
                  </TableCell>
                  <TableCell>
                    {venue.sections?.length ? (
                      <Stack spacing={0.5}>
                        {venue.sections.map((section, index) => (
                          <Typography key={`${section.name}-${index}`} variant="body2">
                            {section.name} ({section.seatCount}, {seatingTypeLabel[section.seatingType]}) ·{' '}
                            {section.price.toLocaleString('sv-SE')} kr
                          </Typography>
                        ))}
                      </Stack>
                    ) : '–'}
                  </TableCell>
                  <TableCell align="right">
                    <Button component={RouterLink} to={`/venues/${venue.id}`}>
                      Redigera
                    </Button>
                    <Button onClick={() => {
                      mutation.reset();
                      setVenueToDelete(venue);
                      setDialogOpen(true);
                    }} color="error">
                      Radera
                    </Button>
                  </TableCell>
                </TableRow>
              ))}
            </TableBody>
          </Table>
        </TableContainer>
      )}
            <Dialog
        open={dialogOpen}
        onClose={() => {
          if (!mutation.isPending) { setDialogOpen(false) }
        }}
      >
        <DialogTitle>Radera scen?</DialogTitle>

        <DialogContent>Är du säker på att du vill radera "{venueToDelete?.name}"?

          {mutation.isError && (
            <Alert severity="error" sx={{ mt: 2 }}>
              Kunde inte radera scenen.
            </Alert>
          )}
        </DialogContent>

        <DialogActions>
          <Button onClick={() => {
            setDialogOpen(false);
          }}
            disabled={mutation.isPending}
          >
            Avbryt
          </Button>

          <Button
            color="error"
            variant="contained"
            disabled={mutation.isPending}
            onClick={() => {
              if (venueToDelete) {
                mutation.mutate(venueToDelete.id);
              }
            }}
          >
            Radera
          </Button>
        </DialogActions>
      </Dialog>
    </>
  );
}