import { useState } from 'react';
import type { ChangeEvent, FormEvent } from 'react';
import { useNavigate, useParams } from 'react-router';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import {
  Alert, Button, CircularProgress, MenuItem, Paper, Stack, TextField, Typography,
} from '@mui/material';
import { createEvent, getEvent, getVenues, updateEvent } from '../api/catalog';
import type { EventDetails } from '../api/catalog';

// Gränserna kommer från backend. UpdateEventRequest tillåter bara 500 tecken i beskrivningen
// (CreateEventRequest 2000), så 500 gäller för båda tills backend rättas.
const MAX_NAME = 200;
const MAX_DESCRIPTION = 500;

// ISO (UTC) -> värde för <input type="datetime-local"> i lokal tid
function toInputValue(iso?: string): string {
  if (!iso) return '';
  const d = new Date(iso);
  const pad = (n: number) => String(n).padStart(2, '0');
  return `${d.getFullYear()}-${pad(d.getMonth() + 1)}-${pad(d.getDate())}T${pad(d.getHours())}:${pad(d.getMinutes())}`;
}

// lokal tid från inputfältet -> ISO (UTC)
function toIso(value: string): string {
  return new Date(value).toISOString();
}

interface FormValues {
  name: string;
  description: string;
  venueId: string;
  startDate: string;
  endDate: string;
  onSaleFrom: string;
  prices: Record<string, string>; // sectionId -> pris som text
}

type FormErrors = Record<string, string>;

function validate(v: FormValues, sectionIds: string[], isEdit: boolean): FormErrors {
  const errors: FormErrors = {};

  if (!v.name.trim()) errors.name = 'Namn krävs';
  else if (v.name.length > MAX_NAME) errors.name = `Max ${MAX_NAME} tecken`;

  if (!v.description.trim()) errors.description = 'Beskrivning krävs';
  else if (v.description.length > MAX_DESCRIPTION) errors.description = `Max ${MAX_DESCRIPTION} tecken`;

  if (!v.startDate) errors.startDate = 'Starttid krävs';

  if (!v.endDate) errors.endDate = 'Sluttid krävs';
  else if (v.startDate && v.endDate <= v.startDate) errors.endDate = 'Sluttid måste vara efter starttid';

  // Scen, biljettsläpp och priser kan bara anges när evenemanget skapas
  if (!isEdit) {
    if (!v.venueId) errors.venueId = 'Välj scen';

    if (!v.onSaleFrom) errors.onSaleFrom = 'Biljettsläpp krävs';
    else if (v.startDate && v.onSaleFrom >= v.startDate) {
      errors.onSaleFrom = 'Försäljningen måste starta före evenemanget';
    }

    sectionIds.forEach((id) => {
      const raw = v.prices[id]?.trim() ?? '';
      const price = Number(raw);
      if (!raw || !Number.isFinite(price) || price < 0) {
        errors[`price-${id}`] = 'Ange ett pris (0 eller mer)';
      }
    });
  }

  return errors;
}

function EventForm({ event }: { event?: EventDetails }) {
  const navigate = useNavigate();
  const queryClient = useQueryClient();
  const isEdit = event !== undefined;

  const { data: venues } = useQuery({ queryKey: ['venues'], queryFn: getVenues });

  const [values, setValues] = useState<FormValues>({
    name: event?.name ?? '',
    description: event?.description ?? '',
    venueId: event?.venueId ?? '',
    startDate: toInputValue(event?.startDate),
    endDate: toInputValue(event?.endDate),
    onSaleFrom: toInputValue(event?.onSaleFrom),
    prices: Object.fromEntries((event?.sectionPrices ?? []).map((p) => [p.sectionId, String(p.price)])),
  });
  const [errors, setErrors] = useState<FormErrors>({});

  // Sektionerna kommer med i scenlistan, så ingen extra hämtning behövs
  const venue = venues?.find((v) => v.id === values.venueId);
  const sections = venue?.sections ?? [];

  const mutation = useMutation({
    mutationFn: async (v: FormValues) => {
      const common = {
        name: v.name.trim(),
        description: v.description.trim(),
        startDate: toIso(v.startDate),
        endDate: toIso(v.endDate),
      };

      if (isEdit) return updateEvent(event.eventId, common);

      return createEvent({
        ...common,
        venueId: v.venueId,
        onSaleFrom: toIso(v.onSaleFrom),
        sectionPrices: sections.map((s) => ({ sectionId: s.id, price: Number(v.prices[s.id]) })),
      });
    },
    onSuccess: async () => {
      await queryClient.invalidateQueries({ queryKey: ['events'] });
      navigate('/events');
    },
  });

  const handleChange =
    (field: Exclude<keyof FormValues, 'prices' | 'venueId'>) => (e: ChangeEvent<HTMLInputElement>) => {
      setValues((prev) => ({ ...prev, [field]: e.target.value }));
    };

  // Byter man scen hör de gamla priserna till andra sektioner, så de nollställs
  const handleVenueChange = (e: ChangeEvent<HTMLInputElement>) => {
    setValues((prev) => ({ ...prev, venueId: e.target.value, prices: {} }));
  };

  const handlePriceChange = (sectionId: string) => (e: ChangeEvent<HTMLInputElement>) => {
    setValues((prev) => ({ ...prev, prices: { ...prev.prices, [sectionId]: e.target.value } }));
  };

  const handleSubmit = (e: FormEvent) => {
    e.preventDefault();
    const found = validate(values, sections.map((s) => s.id), isEdit);
    setErrors(found);
    if (Object.keys(found).length > 0) return;

    mutation.mutate(values);
  };

  const dateFieldProps = { type: 'datetime-local', slotProps: { inputLabel: { shrink: true } } };
  const venueHasNoSections = !isEdit && venue !== undefined && sections.length === 0;

  return (
    <>
      <Typography variant="h4" sx={{ mb: 2 }}>
        {isEdit ? 'Redigera evenemang' : 'Nytt evenemang'}
      </Typography>

      <Paper component="form" onSubmit={handleSubmit} noValidate sx={{ p: 3, maxWidth: 640 }}>
        <Stack spacing={2}>
          {isEdit && (
            <Alert severity="info">
              Scen, biljettsläpp och priser kan inte ändras efter att evenemanget har skapats.
            </Alert>
          )}

          <TextField
            label="Namn" required value={values.name} onChange={handleChange('name')}
            error={!!errors.name} helperText={errors.name}
          />
          <TextField
            label="Beskrivning" required multiline minRows={3}
            value={values.description} onChange={handleChange('description')}
            error={!!errors.description}
            helperText={errors.description ?? `${values.description.length}/${MAX_DESCRIPTION}`}
          />
          <TextField
            select label="Scen" required value={values.venueId}
            onChange={handleVenueChange} disabled={!venues || isEdit}
            error={!!errors.venueId} helperText={errors.venueId}
          >
            {venues?.map((v) => (
              <MenuItem key={v.id} value={v.id}>{v.name}</MenuItem>
            ))}
          </TextField>

          {venueHasNoSections && (
            <Alert severity="warning">
              Scenen har inga sektioner. Lägg till minst en sektion på scenen innan du skapar ett evenemang.
            </Alert>
          )}

          {venue && sections.length > 0 && (
            <Stack spacing={2}>
              <Typography variant="h6">Priser per sektion</Typography>
              {sections.map((section) => (
                <TextField
                  key={section.id}
                  label={`${section.name} (kr)`}
                  required
                  type="number"
                  disabled={isEdit}
                  slotProps={{ htmlInput: { min: 0, step: 0.01 } }}
                  value={values.prices[section.id] ?? ''}
                  onChange={handlePriceChange(section.id)}
                  error={!!errors[`price-${section.id}`]}
                  helperText={errors[`price-${section.id}`]}
                />
              ))}
            </Stack>
          )}

          <TextField
            {...dateFieldProps} label="Start" required value={values.startDate}
            onChange={handleChange('startDate')}
            error={!!errors.startDate} helperText={errors.startDate}
          />
          <TextField
            {...dateFieldProps} label="Slut" required value={values.endDate}
            onChange={handleChange('endDate')}
            error={!!errors.endDate} helperText={errors.endDate}
          />
          <TextField
            {...dateFieldProps} label="Biljettsläpp (försäljning startar)" required
            value={values.onSaleFrom} onChange={handleChange('onSaleFrom')}
            disabled={isEdit}
            error={!!errors.onSaleFrom} helperText={errors.onSaleFrom}
          />

          {mutation.isError && (
            <Alert severity="error">
              Kunde inte spara evenemanget. {mutation.error.message || 'Försök igen.'}
            </Alert>
          )}

          <Stack direction="row" spacing={2} sx={{ justifyContent: 'flex-end' }}>
            <Button onClick={() => navigate('/events')} disabled={mutation.isPending}>Avbryt</Button>
            <Button
              type="submit" variant="contained"
              disabled={mutation.isPending || venueHasNoSections}
            >
              {mutation.isPending ? 'Sparar…' : 'Spara'}
            </Button>
          </Stack>
        </Stack>
      </Paper>
    </>
  );
}

export default function EventFormPage() {
  const { id } = useParams();
  const isEdit = id !== undefined;

  const { data: event, isPending, isError } = useQuery({
    queryKey: ['events', id],
    queryFn: () => getEvent(id!),
    enabled: isEdit,
  });

  if (isEdit && isPending) return <CircularProgress />;
  if (isEdit && isError) return <Alert severity="error">Hittade inte evenemanget.</Alert>;

  return <EventForm event={event} />;
}
