import { useState } from 'react';
import type { ChangeEvent, FormEvent } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import {
    Alert, Box, Button, CircularProgress, MenuItem, Paper, Stack, TextField, Typography,
} from '@mui/material';
import { ApiError } from '../../../shared/api/http';
import { createVenue, getVenue, updateVenue } from '../api/venues';
import type { SaveVenueRequest, SectionSeatingType, Venue } from '../types';
import VenueSeatingMapManager from '../../seating-maps/components/VenueSeatingMapManager';
import { useLocation, useNavigate, useParams } from 'react-router';
import VenueSeatingMapViewer from '../../seating-maps/components/VenueSeatingMapViewer';

const seatingTypeLabel: Record<SectionSeatingType, string> = {
    standing: 'Ståplats',
    seated: 'Sittplats',
};

// id är bara en lokal nyckel för React-listan.
// sectionId finns bara på sektioner som redan är sparade i backend.
interface SectionFormValues {
    id: string;
    sectionId?: string;
    name: string;
    seatCount: string;
    seatingType: SectionSeatingType;
}

interface VenueFormValues {
    name: string;
    streetAddress: string;
    city: string;
    postalCode: string;
    capacity: string;
    sections: SectionFormValues[];
}

type FormErrors = Record<string, string>;

function saveErrorMessage(error: Error): string {
    if (error instanceof ApiError && error.status === 409) {
        const detail = error.message.toLowerCase();

        if (detail.includes('cannot be deleted') || detail.includes('used by an event')) {
            return 'Sektionen används av ett evenemang och kan inte tas bort förrän kopplingen till evenemanget har tagits bort. Priser tas inte bort automatiskt.';
        }
        if (detail.includes('cannot be updated') || detail.includes('capacity and seating type')) {
            return 'Kapacitet och platsform kan inte ändras medan sektionen används av ett evenemang. Sektionsnamnet kan fortfarande ändras.';
        }
        if (detail.includes('combined section capacity')) {
            return 'Sektionernas sammanlagda kapacitet får inte överstiga scenens kapacitet.';
        }
        if (detail.includes('capacity cannot be lower')) {
            return 'Kapaciteten kan inte sänkas under antalet befintliga stolar, och platsformen kan inte ändras medan stolar finns.';
        }
    }

    return error.message || 'Försök igen.';
}

function createSection(): SectionFormValues {
    return {
        id: crypto.randomUUID(),
        name: '',
        seatCount: '1',
        seatingType: 'seated',
    };
}

function toFormValues(venue?: Venue): VenueFormValues {
    if (!venue) {
        return { name: '', streetAddress: '', postalCode: '', city: '', capacity: '', sections: [createSection()] };
    }
    return {
        name: venue.name,
        streetAddress: venue.streetAddress,
        postalCode: venue.postalCode,
        city: venue.city,
        capacity: String(venue.capacity),
        sections: venue.sections.length
            ? venue.sections.map((s) => ({
                id: crypto.randomUUID(),
                sectionId: s.id,
                name: s.name,
                seatCount: String(s.seatCount),
                seatingType: s.seatingType,
            }))
            : [createSection()],
    };
}

function validate(v: VenueFormValues): FormErrors {
    const errors: FormErrors = {};

    if (!v.name.trim()) errors.name = 'Scennamn krävs';
    else if (v.name.length > 200) errors.name = 'Scennamn får vara högst 200 tecken';

    if (!v.streetAddress.trim()) errors.streetAddress = 'Gatuadress krävs';
    else if (v.streetAddress.length > 500) errors.streetAddress = 'Gatuadressen får vara högst 500 tecken';

    if (!v.postalCode.trim()) errors.postalCode = 'Postnummer krävs';
    else if (v.postalCode.length > 20) errors.postalCode = 'Postnumret får vara högst 20 tecken';

    if (!v.city.trim()) errors.city = 'Stad krävs';
    else if (v.city.length > 100) errors.city = 'Staden får vara högst 100 tecken';

    const venueCapacity = Number(v.capacity);
    if (!Number.isInteger(venueCapacity) || venueCapacity < 1) errors.capacity = 'Ange minst 1 plats';

    if (v.sections.length === 0) errors.sections = 'Lägg till minst en sektion';

    v.sections.forEach((section) => {
        if (!section.name.trim()) errors[`section-${section.id}-name`] = 'Namn krävs';
        else if (section.name.length > 100) errors[`section-${section.id}-name`] = 'Max 100 tecken';

        const seatCount = Number(section.seatCount);
        if (!Number.isInteger(seatCount) || seatCount < 1) {
            errors[`section-${section.id}-seatCount`] = 'Ange minst 1 plats';
        }
    });

    const sectionCapacity = v.sections.reduce((total, section) => total + (Number(section.seatCount) || 0), 0);
    if (Number.isInteger(venueCapacity) && venueCapacity > 0 && sectionCapacity > venueCapacity) {
        errors.sections = 'Sektionernas sammanlagda kapacitet får inte överstiga lokalens kapacitet';
    }

    return errors;
}

function VenueForm({ venue }: { venue?: Venue }) {
    const location = useLocation();
    const justCreated = Boolean(location.state?.created);
    const navigate = useNavigate();
    const queryClient = useQueryClient();
    const isEdit = venue !== undefined;

    const [values, setValues] = useState<VenueFormValues>(() => toFormValues(venue));
    const [errors, setErrors] = useState<FormErrors>({});

    const mutation = useMutation({
        mutationFn: (data: SaveVenueRequest) =>
            isEdit ? updateVenue(venue.id, data) : createVenue(data),
        onSuccess: async (savedVenue) => {
            await queryClient.invalidateQueries({ queryKey: ['venues'] });
            if (isEdit) {
                navigate('/venues');
            } else {
                navigate(`/venues/${savedVenue.id}`, { state: { created: true } });
            }
        },
    });

    const handleChange =
        (field: 'name' | 'streetAddress' | 'postalCode' | 'city' | 'capacity') => (e: ChangeEvent<HTMLInputElement>) => {
            setValues((prev) => ({ ...prev, [field]: e.target.value }));
        };

    const handleSectionChange =
        (sectionId: string, field: keyof Omit<SectionFormValues, 'id' | 'sectionId'>) =>
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
            streetAddress: values.streetAddress.trim(),
            postalCode: values.postalCode.trim(),
            city: values.city.trim(),
            capacity: Number(values.capacity),
            sections: values.sections.map((s) => ({
                id: s.sectionId,
                name: s.name.trim(),
                seatCount: Number(s.seatCount),
                seatingType: s.seatingType,
            })),
        });
    };

    const totalCapacity = values.sections.reduce(
        (total, s) => total + (Number(s.seatCount) || 0),
        0,
    );
    const venueCapacity = Number(values.capacity) || 0;
    const remainingCapacity = venueCapacity - totalCapacity;

    return (
        <>
            <Typography variant="h4" sx={{ mb: 2 }}>
                {isEdit ? 'Redigera scen' : 'Ny scen'}
            </Typography>

            {justCreated && (
                <Alert severity="success" sx={{ mb: 2 }}>
                    Scenen är sparad. Ladda upp en platskarta i panelen till höger och justera sektionerna med kartan bredvid.
                </Alert>
            )}

            <Stack
                direction={{ xs: 'column', lg: 'row' }}
                spacing={3}
                sx={{ alignItems: { xs: 'stretch', lg: 'flex-start' } }}
            >
                <Paper
                    component="form"
                    onSubmit={handleSubmit}
                    noValidate
                    sx={{ p: 3, flex: 1, minWidth: 0, maxWidth: 900 }}
                >
                    <Stack spacing={3}>
                        <TextField
                            label="Scennamn" required value={values.name} onChange={handleChange('name')}
                            error={!!errors.name} helperText={errors.name}
                        />
                        <TextField
                            label="Gatuadress" required value={values.streetAddress} onChange={handleChange('streetAddress')}
                            error={!!errors.streetAddress} helperText={errors.streetAddress}
                        />
                        <TextField
                            label="Postnummer" required value={values.postalCode} onChange={handleChange('postalCode')}
                            error={!!errors.postalCode} helperText={errors.postalCode}
                        />
                        <TextField
                            label="Stad" required value={values.city} onChange={handleChange('city')}
                            error={!!errors.city} helperText={errors.city}
                        />

                        <Paper variant="outlined" sx={{ p: 2 }}>
                            <Stack spacing={1}>
                                <Typography variant="h6">Lokalens kapacitet</Typography>
                                <Typography variant="body2" color="text.secondary">
                                    Högsta antal personer som lokalen får ta emot.
                                </Typography>
                                <TextField
                                    label="Antal platser i lokalen" required type="number" fullWidth
                                    slotProps={{ htmlInput: { min: 1, step: 1 } }}
                                    value={values.capacity} onChange={handleChange('capacity')}
                                    error={!!errors.capacity} helperText={errors.capacity}
                                />
                            </Stack>
                        </Paper>

                        <Box>
                            <Box sx={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', mb: 1 }}>
                                <Typography variant="h6">Sektioner</Typography>
                                <Button onClick={addSection}>Lägg till sektion</Button>
                            </Box>

                            {isEdit && (
                                <Alert severity="info" sx={{ mb: 2 }}>
                                    Sektionsnamn kan ändras. Kapacitet och platsform kan inte ändras medan sektionen används
                                    av ett evenemang eller har stolar som hindrar ändringen. En sektion kan inte tas bort
                                    medan den används av ett evenemang; ta först bort kopplingen till sektionen från evenemanget.
                                    Borttagning raderar inte priser automatiskt.
                                </Alert>
                            )}

                            {errors.sections && <Alert severity="error" sx={{ mb: 2 }}>{errors.sections}</Alert>}

                            <Stack spacing={2}>
                                {values.sections.map((section, index) => {
                                    return (
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
                                                </Stack>
                                            </Stack>
                                        </Paper>
                                    );
                                })}
                            </Stack>

                            <Typography variant="body2" color="text.secondary" sx={{ mt: 1 }}>
                                Sektionernas kapacitet: {totalCapacity.toLocaleString('sv-SE')} av {venueCapacity.toLocaleString('sv-SE')} platser
                                {' · '}Platser kvar att använda: {remainingCapacity.toLocaleString('sv-SE')}
                            </Typography>
                        </Box>

                        {mutation.isError && (
                            <Alert severity="error">
                                Kunde inte spara scenen. {saveErrorMessage(mutation.error)}
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

                <Box
                    sx={{
                        position: { xs: 'static', lg: 'sticky' },
                        top: 88,
                        width: { xs: '100%', lg: 420 },
                        flexShrink: 0,
                        maxHeight: { xs: 'none', lg: 'calc(100vh - 104px)' },
                        overflow: { xs: 'visible', lg: 'auto' },
                    }}
                >
                    {isEdit ? (
                        <Stack spacing={2}>
                            <VenueSeatingMapViewer venueId={venue.id} />
                            <VenueSeatingMapManager venueId={venue.id} />
                        </Stack>
                    ) : (
                        <Alert severity="info">
                            Spara scenen först för att kunna lägga till platskartor.
                        </Alert>
                    )}
                </Box>
            </Stack>
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
