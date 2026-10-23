import { Box, Typography } from '@mui/material';

const MONTHS_SV = ['jan', 'feb', 'mar', 'apr', 'maj', 'jun', 'jul', 'aug', 'sep', 'okt', 'nov', 'dec'];

interface DateBadgeProps {
  startDate: string;
  endDate: string;
  compact?: boolean;
}

export default function DateBadge({ startDate, endDate, compact = false }: DateBadgeProps) {
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
        borderRadius: compact ? '4px' : 2,
        px: 1.5,
        py: 1,
        minWidth: 56,
        textAlign: 'center',
        lineHeight: 1.1,
        ...(compact && {
          width: '100%',
          height: 40,
          boxSizing: 'border-box',
          px: 1,
          py: 0,
          display: 'flex',
          flexDirection: 'row',
          alignItems: 'center',
          justifyContent: 'center',
          gap: 0.75,
        }),
      }}
    >
      <Typography component="div" sx={{ fontSize: compact ? '1rem' : '1.75rem', fontWeight: 700, lineHeight: 1 }}>
        {day}
      </Typography>
      <Typography component="div" variant="caption" sx={{ textTransform: 'uppercase', letterSpacing: compact ? 0.5 : 1 }}>
        {month}
      </Typography>
    </Box>
  );
}
