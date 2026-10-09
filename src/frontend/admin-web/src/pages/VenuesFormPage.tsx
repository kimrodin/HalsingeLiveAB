import { useState } from 'react';
import type { ChangeEvent, FormEvent } from 'react';
import { useNavigate, useParams } from 'react-router';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import {
  Alert, Box, Button, CircularProgress, MenuItem, Paper, Stack, TextField, Typography,
} from '@mui/material';
import { createVenue, getVenue, updateVenue } from '../api/catalog';
import type { CreateVenueRequest, SectionSeatingType, Venue } from '../api/catalog';

const seatingTypeLabel: Record<SectionSeatingType, string> = {
  standing: 'Ståplats',
  seated: 'Sittplats',
};

// id är bara en lokal nyckel för React-listan, det skickas inte till API:t
interface SectionFormValues {
  id: string;
  name: string;
  seatCount: string;
  seatingType: SectionSeatingType;
  price: string;
}

interface VenueFormValues {
  name: string;
  address: string;
  sections: SectionFormValues[];
}

type FormErrors = Record<string, string>;

function createSection(): SectionFormValues {
  return {
    id: crypto.randomUUID(),
    name: '',
    seatCount: '1',
    seatingType: 'seated',
    price: '0',
  };
}

function toFormValues(venue?: Venue): VenueFormValues {
  if (!venue) {
    return { name: '', address: '', sections: [createSection()] };
  }
  return {
    name: venue.name,
    address: venue.address ?? '',
    sections: venue.sections?.length
      ? venue.sections.map((s) => ({
          id: crypto.randomUUID(),
          name: s.name,
          seatCount: String(s.seatCount),
          seatingType: s.seatingType,
          price: String(s.price),
        }))
      : [createSection()],
  };
}

function validate(v: VenueFormValues): FormErrors {
  const errors: FormErrors = {};
  if (!v.name.trim()) errors.name = 'Scennamn krävs';
  if (!v.address.trim()) errors.address = 'Adress krävs';
  if (v.sections.length === 0) errors.sections = 'Lägg till minst en sektion';

  v.sections.forEach((section) => {
    if (!section.name.trim()) errors[`section-${section.id}-name`] = 'Namn krävs';

    const seatCount = Number(section.seatCount);
    if (!Number.isInteger(seatCount) || seatCount < 1) {
      errors[`section-${section.id}-seatCount`] = 'Ange minst 1 plats';
    }

    const price = Number(section.price);
    if (!section.price.trim() || !Number.isFinite(price) || price < 0) {
      errors[`section-${section.id}-price`] = 'Ange ett giltigt pris (0 kr eller mer)';
    }
  });

  return errors;
}

function VenueForm({ venue }: { venue?: Venue }) {
  const navigate = useNavigate();
  const queryClient = useQueryClient();
  const isEdit = venue !== undefined;

  const [values, setValues] = useState<VenueFormValues>(() => toFormValues(venue));
  const [errors, setErrors] = useState<FormErrors>({});

  const mutation = useMutation({
    mutationFn: (data: CreateVenueRequest) =>
      isEdit ? updateVenue(venue.id, data) : createVenue(data),
    onSuccess: async () => {
      await queryClient.invalidateQueries({ queryKey: ['venues'] });
      navigate('/venues');
    },
  });

  const handleChange =
    (field: 'name' | 'address') => (e: ChangeEvent<HTMLInputElement>) => {
      setValues((prev) => ({ ...prev, [field]: e.target.value }));
    };

  const handleSectionChange =
    (sectionId: string, field: keyof Omit<SectionFormValues, 'id'>) =>
    (e: ChangeEvent<HTMLInputElement>) => {
      setValues((prev) => ({
        ...prev,
        sections: prev.sections.map((s) =>
          s.id === sectionId ? { ...s, [field]: e.target.value } : s,
        ),
      }));
    };

  const addSection = () => {
    setValues((prev) => ({ ...prev, sections: [...prev.sections, createSection()] }));
  };

  const removeSection = (sectionId: string) => {
    setValues((prev) => ({
      ...prev,
      sections: prev.sections.filter((s) => s.id !== sectionId),
    }));
  };

  const handleSubmit = (e: FormEvent) => {
    e.preventDefault();
    const found = validate(values);
    setErrors(found);
    if (Object.keys(found).length > 0) return;

    mutation.mutate({
      name: values.name.trim(),
      address: values.address.trim(),
      sections: values.sections.map((s) => ({
        name: s.name.trim(),
        seatCount: Number(s.seatCount),
        seatingType: s.seatingType,
        price: Number(s.price),
      })),
    });
  };

  const totalCapacity = values.sections.reduce(
    (total, s) => total + (Number(s.seatCount) || 0),
    0,
  );

  return (
    <>
      <Typography variant="h4" sx={{ mb: 2 }}>
        {isEdit ? 'Redigera scen' : 'Ny scen'}
      </Typography>

      <Paper component="form" onSubmit={handleSubmit} noValidate sx={{ p: 3, maxWidth: 900 }}>
        <Stack spacing={3}>
          <TextField
            label="Scennamn" required value={values.name} onChange={handleChange('name')}
            error={!!errors.name} helperText={errors.name}
          />
          <TextField
            label="Adress" required value={values.address} onChange={handleChange('address')}
            error={!!errors.address} helperText={errors.address}
          />

          <Box>
            <Box sx={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', mb: 1 }}>
              <Typography variant="h6">Sektioner</Typography>
              <Button onClick={addSection}>Lägg till sektion</Button>
            </Box>

            {errors.sections && <Alert severity="error" sx={{ mb: 2 }}>{errors.sections}</Alert>}

            <Stack spacing={2}>
              {values.sections.map((section, index) => (
                <Paper key={section.id} variant="outlined" sx={{ p: 2 }}>
                  <Stack spacing={2}>
                    <Box sx={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
                      <Typography variant="subtitle1">Sektion {index + 1}</Typography>
                      <Button
                        color="error"
                        disabled={values.sections.length === 1}
                        onClick={() => removeSection(section.id)}
                      >
                        Ta bort
                      </Button>
                    </Box>

                    <Stack direction={{ xs: 'column', md: 'row' }} spacing={2}>
                      <TextField
                        label="Sektionsnamn" required fullWidth value={section.name}
                        onChange={handleSectionChange(section.id, 'name')}
                        error={!!errors[`section-${section.id}-name`]}
                        helperText={errors[`section-${section.id}-name`]}
                      />
                      <TextField
                        label="Antal platser" required type="number" fullWidth
                        slotProps={{ htmlInput: { min: 1, step: 1 } }}
                        value={section.seatCount}
                        onChange={handleSectionChange(section.id, 'seatCount')}
                        error={!!errors[`section-${section.id}-seatCount`]}
                        helperText={errors[`section-${section.id}-seatCount`]}
                      />
                    </Stack>

                    <Stack direction={{ xs: 'column', md: 'row' }} spacing={2}>
                      <TextField
                        select label="Platsform" fullWidth value={section.seatingType}
                        onChange={handleSectionChange(section.id, 'seatingType')}
                      >
                        {Object.entries(seatingTypeLabel).map(([type, label]) => (
                          <MenuItem key={type} value={type}>{label}</MenuItem>
                        ))}
                      </TextField>
                      <TextField
                        label="Pris per sektion (kr)" required type="number" fullWidth
                        slotProps={{ htmlInput: { min: 0, step: 0.01 } }}
                        value={section.price}
                        onChange={handleSectionChange(section.id, 'price')}
                        error={!!errors[`section-${section.id}-price`]}
                        helperText={errors[`section-${section.id}-price`]}
                      />
                    </Stack>
                  </Stack>
                </Paper>
              ))}
            </Stack>

            <Typography variant="body2" color="text.secondary" sx={{ mt: 1 }}>
              Total kapacitet: {totalCapacity.toLocaleString('sv-SE')} platser
            </Typography>
          </Box>

          {mutation.isError && (
            <Alert severity="error">
              Kunde inte spara scenen. {mutation.error.message || 'Försök igen.'}
            </Alert>
          )}

          <Stack direction="row" spacing={2} sx={{ justifyContent: 'flex-end' }}>
            <Button onClick={() => navigate('/venues')} disabled={mutation.isPending}>
              Avbryt
            </Button>
            <Button type="submit" variant="contained" disabled={mutation.isPending}>
              {mutation.isPending ? 'Sparar…' : 'Spara scen'}
            </Button>
          </Stack>
        </Stack>
      </Paper>
    </>
  );
}

export default function VenueFormPage() {
  const { id } = useParams();
  const isEdit = id !== undefined;

  const { data: venue, isPending, isError } = useQuery({
    queryKey: ['venues', id],
    queryFn: () => getVenue(id!),
    enabled: isEdit,
  });

  if (isEdit && isPending) return <CircularProgress />;
  if (isEdit && isError) return <Alert severity="error">Hittade inte scenen.</Alert>;

  return <VenueForm venue={venue} />;
}