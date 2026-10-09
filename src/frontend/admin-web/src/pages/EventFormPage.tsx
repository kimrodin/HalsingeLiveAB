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
  name: string;
  description: string;
  category: EventCategory | '';
  venueId: string;
  price: string;
  startDate: string;
  endDate: string;
  onSaleFrom: string;
}

type FormErrors = Partial<Record<keyof FormValues, string>>;

function validate(v: FormValues): FormErrors {
  const errors: FormErrors = {};
  if (!v.name.trim()) errors.name = 'Namn krävs';
  else if (v.name.length > 200) errors.name = 'Max 200 tecken';
  if (!v.category) errors.category = 'Välj kategori';
  if (!v.venueId) errors.venueId = 'Välj scen';
  if (!v.price.trim()) errors.price = 'Pris krävs';
  else if (!Number.isFinite(Number(v.price)) || Number(v.price) < 0) {
    errors.price = 'Ange ett giltigt pris i kronor';
  }
  if (!v.startDate) errors.startDate = 'Starttid krävs';
  if (v.endDate && v.startDate && v.endDate <= v.startDate) {
    errors.endDate = 'Sluttid måste vara efter starttid';
  }
  if (v.onSaleFrom && v.startDate && v.onSaleFrom >= v.startDate) {
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
    name: event?.name ?? '',
    description: event?.description ?? '',
    category: event?.category ?? '',
    venueId: event?.venueId ?? '',
    price: event?.price.toString() ?? '',
    startDate: toInputValue(event?.startDate),
    endDate: toInputValue(event?.endDate),
    onSaleFrom: toInputValue(event?.onSaleFrom),
  });
  const [errors, setErrors] = useState<FormErrors>({});

  const mutation = useMutation({
    mutationFn: (data: CreateEventRequest) =>
      isEdit ? updateEvent(event.eventId, data) : createEvent(data),
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
      name: values.name.trim(),
      description: values.description.trim() || undefined,
      category: values.category as EventCategory,
      venueId: values.venueId,
      price: Number(values.price),
      startDate: toIso(values.startDate)!,
      endDate: toIso(values.endDate),
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
            label="Namn" required value={values.name} onChange={handleChange('name')}
            error={!!errors.name} helperText={errors.name}
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
            label="Pris (kr)" required type="number" slotProps={{ htmlInput: { min: 0, step: 0.01 } }}
            value={values.price} onChange={handleChange('price')}
            error={!!errors.price} helperText={errors.price}
          />
          <TextField
            {...dateFieldProps} label="Start" required value={values.startDate}
            onChange={handleChange('startDate')}
            error={!!errors.startDate} helperText={errors.startDate}
          />
          <TextField
            {...dateFieldProps} label="Slut" value={values.endDate}
            onChange={handleChange('endDate')}
            error={!!errors.endDate} helperText={errors.endDate}
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