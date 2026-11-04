import { useState } from 'react';
import { Alert, Box, Button, Paper, TextField, Typography } from '@mui/material';
import { last30Days, validatePeriod } from '../utils/dashboardPeriod';
import type { DashboardPeriod } from '../utils/dashboardPeriod';

interface Props {
  period: DashboardPeriod;
  onApply: (period: DashboardPeriod) => void;
}

export default function DashboardPeriodFilter({ period, onApply }: Props) {
  const [draft, setDraft] = useState(period);
  const error = validatePeriod(draft);
  const dateInput = { min: '0002-01-01', max: '9999-12-30', style: { colorScheme: 'dark' } };

  return (
    <Paper component="form" noValidate onSubmit={event => {
      event.preventDefault();
      if (!error) onApply(draft);
    }} sx={{ p: 2.5, mb: 3 }} aria-label="Välj dashboardens period">
      <Typography component="h2" variant="h6" sx={{ mb: 2 }}>Period</Typography>
      <Box sx={{ display: 'flex', flexWrap: 'wrap', gap: 2, alignItems: 'center' }}>
        <TextField label="Från" type="date" value={draft.from} error={Boolean(error)}
          onChange={event => setDraft(v => ({ ...v, from: event.target.value }))}
          slotProps={{ inputLabel: { shrink: true }, htmlInput: dateInput }}
          sx={{ flex: { xs: '1 1 100%', sm: '0 1 190px' } }} />
        <TextField label="Till" type="date" value={draft.to} error={Boolean(error)}
          onChange={event => setDraft(v => ({ ...v, to: event.target.value }))}
          slotProps={{ inputLabel: { shrink: true }, htmlInput: { ...dateInput, min: draft.from || dateInput.min } }}
          sx={{ flex: { xs: '1 1 100%', sm: '0 1 190px' } }} />
        <Button type="submit" variant="contained" disabled={Boolean(error)} sx={{ minHeight: 56 }}>Visa period</Button>
        <Button type="button" variant="outlined" onClick={() => {
          const next = last30Days();
          setDraft(next);
          onApply(next);
        }}>Senaste 30 dagarna</Button>
      </Box>
      {error && <Alert severity="warning" sx={{ mt: 2 }}>{error}</Alert>}
    </Paper>
  );
}
