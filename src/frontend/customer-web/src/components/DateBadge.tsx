import { Box, Typography } from '@mui/material';

const MONTHS_SV = ['jan', 'feb', 'mar', 'apr', 'maj', 'jun', 'jul', 'aug', 'sep', 'okt', 'nov', 'dec'];

interface DateBadgeProps {
  startDate: string;
  endDate: string;
}

export default function DateBadge({ startDate, endDate }: DateBadgeProps) {
  const start = new Date(startDate);
  const end = new Date(endDate);

  const sameDay = start.toDateString() === end.toDateString();
  const day = sameDay ? `${start.getDate()}` : `${start.getDate()}–${end.getDate()}`;

  // Flerdagarsevenemang som går över månadsskifte visar båda månaderna
  const sameMonth = start.getMonth() === end.getMonth();
  const month = sameDay || sameMonth
    ? MONTHS_SV[start.getMonth()]
    : `${MONTHS_SV[start.getMonth()]}–${MONTHS_SV[end.getMonth()]}`;

  return (
    <Box
      sx={{
        bgcolor: 'secondary.main',
        color: 'secondary.contrastText',
        borderRadius: 2,
        px: 1.5,
        py: 1,
        minWidth: 56,
        textAlign: 'center',
        lineHeight: 1.1,
      }}
    >
      <Typography component="div" sx={{ fontSize: '1.75rem', fontWeight: 700, lineHeight: 1 }}>
        {day}
      </Typography>
      <Typography component="div" variant="caption" sx={{ textTransform: 'uppercase', letterSpacing: 1 }}>
        {month}
      </Typography>
    </Box>
  );
}