import { useState } from 'react';
import type { FormEvent } from 'react';
import { useNavigate, useParams } from 'react-router';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import {
  Alert, Button, CircularProgress, MenuItem, Paper, Stack, TextField, Typography,
} from '@mui/material';
import {
  createEvent, getEvent, getVenues, updateEvent,
} from '../api/catalog';
import type { CreateEventRequest, EventCategory, EventDetails } from '../api/catalog';

const categoryLabel: Record<EventCategory, string> = {
  concert: 'Konsert',
  standup: 'Ståuppshow',
  conference: 'Konferens',
  workshop: 'Workshop',
};

// ISO (UTC) -> värde för <input type="datetime-local"> i lokal tid
function toInputValue(iso?: string): string {
  if (!iso) return '';
  const d = new Date(iso);
  const pad = (n: number) => String(n).padStart(2, '0');
  return `${d.getFullYear()}-${pad(d.getMonth() + 1)}-${pad(d.getDate())}T${pad(d.getHours())}:${pad(d.getMinutes())}`;
}

// lokal tid från inputfältet -> ISO (UTC)
function toIso(value: string): string | undefined {
  return value ? new Date(value).toISOString() : undefined;
}

interface FormValues {
  title: string;
  description: string;
  category: EventCategory | '';
  venueId: string;
  startsAt: string;
  endsAt: string;
  onSaleFrom: string;
}

type FormErrors = Partial<Record<keyof FormValues, string>>;

function validate(v: FormValues): FormErrors {
  const errors: FormErrors = {};
  if (!v.title.trim()) errors.title = 'Titel krävs';
  else if (v.title.length > 120) errors.title = 'Max 120 tecken';
  if (!v.category) errors.category = 'Välj kategori';
  if (!v.venueId) errors.venueId = 'Välj scen';
  if (!v.startsAt) errors.startsAt = 'Starttid krävs';
  if (v.endsAt && v.startsAt && v.endsAt <= v.startsAt) {
    errors.endsAt = 'Sluttid måste vara efter starttid';
  }
  if (v.onSaleFrom && v.startsAt && v.onSaleFrom >= v.startsAt) {
    errors.onSaleFrom = 'Försäljningen måste starta före evenemanget';
  }
  return errors;
}

function EventForm({ event }: { event?: EventDetails }) {
  const navigate = useNavigate();
  const queryClient = useQueryClient();
  const isEdit = event !== undefined;

  const { data: venues } = useQuery({ queryKey: ['venues'], queryFn: getVenues });

  const [values, setValues] = useState<FormValues>({
    title: event?.title ?? '',
    description: event?.description ?? '',
    category: event?.category ?? '',
    venueId: event?.venueId ?? '',
    startsAt: toInputValue(event?.startsAt),
    endsAt: toInputValue(event?.endsAt),
    onSaleFrom: toInputValue(event?.onSaleFrom),
  });
  const [errors, setErrors] = useState<FormErrors>({});

  const mutation = useMutation({
    mutationFn: (data: CreateEventRequest) =>
      isEdit ? updateEvent(event.id, data) : createEvent(data),
    onSuccess: async () => {
      await queryClient.invalidateQueries({ queryKey: ['events'] });
      navigate('/events');
    },
  });

  const handleChange =
    (field: keyof FormValues) => (e: React.ChangeEvent<HTMLInputElement>) => {
      setValues((prev) => ({ ...prev, [field]: e.target.value }));
    };

  const handleSubmit = (e: FormEvent) => {
    e.preventDefault();
    const found = validate(values);
    setErrors(found);
    if (Object.keys(found).length > 0) return;

    mutation.mutate({
      title: values.title.trim(),
      description: values.description.trim() || undefined,
      category: values.category as EventCategory,
      venueId: values.venueId,
      startsAt: toIso(values.startsAt)!,
      endsAt: toIso(values.endsAt),
      onSaleFrom: toIso(values.onSaleFrom),
    });
  };

  const dateFieldProps = { type: 'datetime-local', slotProps: { inputLabel: { shrink: true } } };

  return (
    <>
      <Typography variant="h4" sx={{ mb: 2 }}>
        {isEdit ? 'Redigera evenemang' : 'Nytt evenemang'}
      </Typography>

      <Paper component="form" onSubmit={handleSubmit} noValidate sx={{ p: 3, maxWidth: 640 }}>
        <Stack spacing={2}>
          <TextField
            label="Titel" required value={values.title} onChange={handleChange('title')}
            error={!!errors.title} helperText={errors.title}
          />
          <TextField
            label="Beskrivning" multiline minRows={3}
            value={values.description} onChange={handleChange('description')}
          />
          <TextField
            select label="Kategori" required value={values.category}
            onChange={handleChange('category')}
            error={!!errors.category} helperText={errors.category}
          >
            {Object.entries(categoryLabel).map(([value, label]) => (
              <MenuItem key={value} value={value}>{label}</MenuItem>
            ))}
          </TextField>
          <TextField
            select label="Scen" required value={values.venueId}
            onChange={handleChange('venueId')} disabled={!venues}
            error={!!errors.venueId} helperText={errors.venueId}
          >
            {venues?.map((v) => (
              <MenuItem key={v.id} value={v.id}>{v.name}</MenuItem>
            ))}
          </TextField>
          <TextField
            {...dateFieldProps} label="Start" required value={values.startsAt}
            onChange={handleChange('startsAt')}
            error={!!errors.startsAt} helperText={errors.startsAt}
          />
          <TextField
            {...dateFieldProps} label="Slut" value={values.endsAt}
            onChange={handleChange('endsAt')}
            error={!!errors.endsAt} helperText={errors.endsAt}
          />
          <TextField
            {...dateFieldProps} label="Biljettsläpp (försäljning startar)" value={values.onSaleFrom}
            onChange={handleChange('onSaleFrom')}
            error={!!errors.onSaleFrom} helperText={errors.onSaleFrom}
          />

          {mutation.isError && (
            <Alert severity="error">Kunde inte spara evenemanget. Försök igen.</Alert>
          )}

          <Stack direction="row" spacing={2} sx={{ justifyContent: 'flex-end' }}>
            <Button onClick={() => navigate('/events')}>Avbryt</Button>
            <Button type="submit" variant="contained" disabled={mutation.isPending}>
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