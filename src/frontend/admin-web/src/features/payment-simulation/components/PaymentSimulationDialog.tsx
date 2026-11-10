import { useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import {
  Alert, Button, CircularProgress, Dialog, DialogActions, DialogContent,
  DialogTitle, MenuItem, Stack, TextField,
} from '@mui/material';
import {
  getPaymentSimulation, updatePaymentSimulation,
  type PaymentSimulationSettings,
} from '../api/paymentSimulation';

const queryKey = ['payment-simulation'];

interface Props {
  onClose: () => void;
  onSaved: () => void;
}

export default function PaymentSimulationDialog({ onClose, onSaved }: Props) {
  const settings = useQuery({
    queryKey,
    queryFn: getPaymentSimulation,
    refetchOnWindowFocus: false,
    retry: false,
  });

  return (
    <Dialog open onClose={onClose} fullWidth maxWidth="sm" aria-labelledby="payment-simulation-title">
      <DialogTitle id="payment-simulation-title">Betalningssimulator</DialogTitle>
      {settings.isPending || settings.isFetching ? (
        <DialogContent><CircularProgress aria-label="Hämtar inställningar" /></DialogContent>
      ) : settings.isError ? (
        <>
          <DialogContent>
            <Alert severity="error">Kunde inte hämta inställningarna: {settings.error.message}</Alert>
          </DialogContent>
          <DialogActions>
            <Button onClick={onClose}>Stäng</Button>
            <Button onClick={() => void settings.refetch()}>Försök igen</Button>
          </DialogActions>
        </>
      ) : (
        <SettingsForm initial={settings.data} onClose={onClose} onSaved={onSaved} />
      )}
    </Dialog>
  );
}

function SettingsForm({ initial, onClose, onSaved }: Props & { initial: PaymentSimulationSettings }) {
  const queryClient = useQueryClient();
  const [outcome, setOutcome] = useState(initial.outcome);
  const [delay, setDelay] = useState(String(initial.delayMs));
  const [timeout, setTimeout] = useState(String(initial.timeoutMs));
  const delayMs = Number(delay);
  const timeoutMs = Number(timeout);
  const delayValid = delay.trim() !== '' && Number.isInteger(delayMs) && delayMs >= 0 && delayMs <= 2147483647;
  const timeoutValid = timeout.trim() !== '' && Number.isInteger(timeoutMs) && timeoutMs >= 1 && timeoutMs <= 30000;
  const mutation = useMutation({
    mutationFn: updatePaymentSimulation,
    onSuccess: (saved) => {
      queryClient.setQueryData(queryKey, saved);
      onSaved();
    },
  });

  return (
    <form onSubmit={(event) => {
      event.preventDefault();
      if (delayValid && timeoutValid && !mutation.isPending) mutation.mutate({ outcome, delayMs, timeoutMs });
    }}>
      <DialogContent>
        <Stack spacing={2}>
          <Alert severity="info">
            Gäller alla nya betalningsförsök. Befintliga betalningar ändras inte.
            Skapa en ny bokning för att testa ett nytt utfall.
          </Alert>
          {mutation.isError && <Alert severity="error">Kunde inte spara: {mutation.error.message}</Alert>}
          <TextField select label="Betalningsutfall" value={outcome} disabled={mutation.isPending}
            onChange={(event) => setOutcome(event.target.value as PaymentSimulationSettings['outcome'])}>
            <MenuItem value="Succeeded">Lyckad (Succeeded)</MenuItem>
            <MenuItem value="Failed">Misslyckad (Failed)</MenuItem>
            <MenuItem value="TimedOut">Timeout (TimedOut)</MenuItem>
            <MenuItem value="Random">Slumpmässigt (Random)</MenuItem>
          </TextField>
          {outcome === 'Random' && (
            <Alert severity="info">Varje nytt betalningsförsök har 80 % chans till Succeeded, 15 % till Failed och 5 % till TimedOut. Återförsök behåller samma utfall.</Alert>
          )}
          <TextField label="Fördröjning (ms)" type="number" required value={delay}
            disabled={mutation.isPending} onChange={(event) => setDelay(event.target.value)}
            error={!delayValid} helperText="Heltal från 0 till 2147483647."
            slotProps={{ htmlInput: { min: 0, max: 2147483647, step: 1 } }} />
          <TextField label="Timeout (ms)" type="number" required value={timeout}
            disabled={mutation.isPending} onChange={(event) => setTimeout(event.target.value)}
            error={!timeoutValid} helperText="Heltal mellan 1 och 30000."
            slotProps={{ htmlInput: { min: 1, max: 30000, step: 1 } }} />
          {delayValid && timeoutValid && delayMs > timeoutMs && (
            <Alert severity="warning">Fördröjningen överstiger timeout. Nya betalningar får utfallet TimedOut.</Alert>
          )}
        </Stack>
      </DialogContent>
      <DialogActions>
        <Button onClick={onClose} disabled={mutation.isPending}>Avbryt</Button>
        <Button type="submit" variant="contained" disabled={!delayValid || !timeoutValid || mutation.isPending}>
          {mutation.isPending ? 'Sparar…' : 'Spara'}
        </Button>
      </DialogActions>
    </form>
  );
}
