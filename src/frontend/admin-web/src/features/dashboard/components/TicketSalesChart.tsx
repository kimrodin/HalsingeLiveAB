import { useId } from 'react';
import { Box, Typography } from '@mui/material';
import { useTheme } from '@mui/material/styles';
import type { SalesStatistics } from '../api/salesStatistics';
import { formatDate } from '../utils/dashboardPeriod';

type Day = SalesStatistics['daily'][number];
const number = new Intl.NumberFormat('sv-SE');
const shortDay = new Intl.DateTimeFormat('sv-SE', { day: 'numeric', month: 'short', timeZone: 'Europe/Stockholm' });

export default function TicketSalesChart({ daily }: { daily: Day[] }) {
  const theme = useTheme();
  const id = useId();
  if (daily.length === 0) return <Typography color="text.secondary">Det finns inga dagsvärden för perioden.</Typography>;

  const total = daily.reduce((sum, day) => sum + day.soldTickets, 0);
  const peak = daily.reduce((best, day) => day.soldTickets > best.soldTickets ? day : best);
  const maximum = Math.max(4, Math.ceil(peak.soldTickets / 4) * 4);
  const top = 18;
  const bottom = 216;
  const x = (i: number) => daily.length === 1 ? 500 : 40 + i / (daily.length - 1) * 920;
  const y = (value: number) => bottom - value / maximum * (bottom - top);
  const line = daily.map((day, i) => `${i === 0 ? 'M' : 'L'}${x(i)},${y(day.soldTickets)}`).join(' ');
  const area = `${line} L${x(daily.length - 1)},${bottom} L${x(0)},${bottom} Z`;
  const ticks = [maximum, maximum * 0.75, maximum * 0.5, maximum * 0.25, 0];
  const labels = [...new Set([0, Math.floor((daily.length - 1) / 2), daily.length - 1])];

  return (
    <>
      {total === 0 && <Typography variant="body2" color="text.secondary">Ingen nettobiljettförsäljning under perioden.</Typography>}
      <Box sx={{ display: 'flex', gap: 1 }}>
        <Box aria-hidden="true" sx={{ width: 48, flexShrink: 0, position: 'relative', height: 240 }}>
          {ticks.map(value => <Typography key={value} variant="caption" color="text.secondary" sx={{ position: 'absolute', right: 0, top: y(value), transform: 'translateY(-50%)' }}>{number.format(value)}</Typography>)}
        </Box>
        <Box tabIndex={0} role="region" aria-label="Biljettförsäljning, rulla i sidled för fler dagar" sx={{ flex: 1, minWidth: 0, overflowX: 'auto' }}>
          <Box sx={{ minWidth: Math.max(200, daily.length * 24) }}>
          <svg width="100%" height="240" viewBox="0 0 1000 240" preserveAspectRatio="none"
            role="img" aria-labelledby={`${id}-title ${id}-description`} style={{ display: 'block' }}>
            <title id={`${id}-title`}>Sålda biljetter per dag</title>
            <desc id={`${id}-description`}>{formatDate(daily[0].date)} till {formatDate(daily[daily.length - 1].date)}. Totalt {number.format(total)} biljetter efter återbetalningar. Högst {number.format(peak.soldTickets)} biljetter på en dag.</desc>
            <defs><linearGradient id={`${id}-fill`} x1="0" y1="0" x2="0" y2="1"><stop stopColor={theme.palette.primary.main} stopOpacity="0.3" /><stop offset="1" stopColor={theme.palette.primary.main} stopOpacity="0.02" /></linearGradient></defs>
            {ticks.map(value => <line key={value} x1="0" x2="1000" y1={y(value)} y2={y(value)} stroke={theme.palette.divider} vectorEffect="non-scaling-stroke" />)}
            {daily.map((day, i) => <g key={day.date}>
              <title>{formatDate(day.date)}</title>
              <line x1={x(i)} x2={x(i)} y1={top} y2={bottom} stroke={theme.palette.text.secondary} strokeOpacity="0.25" vectorEffect="non-scaling-stroke" />
              <line x1={x(i)} x2={x(i)} y1={bottom} y2={bottom + 6} stroke={theme.palette.text.secondary} vectorEffect="non-scaling-stroke" />
            </g>)}
            <path d={area} fill={`url(#${id}-fill)`} />
            <path d={line} fill="none" stroke={theme.palette.primary.main} strokeWidth="2" vectorEffect="non-scaling-stroke" />
            {daily.length === 1 && <circle cx={x(0)} cy={y(daily[0].soldTickets)} r="5" fill={theme.palette.primary.light} />}
          </svg>
          <Box aria-hidden="true" sx={{ position: 'relative', height: 22 }}>
            {daily.map((day, i) => <Typography key={day.date} variant="caption" color="text.secondary" title={formatDate(day.date)}
              sx={{ position: 'absolute', left: `${x(i) / 10}%`, transform: 'translateX(-50%)' }}>{Number(day.date.slice(8, 10))}</Typography>)}
          </Box>
          <Box aria-hidden="true" sx={{ display: 'flex', justifyContent: labels.length === 1 ? 'center' : 'space-between', gap: 1 }}>
            {labels.map(i => <Typography key={i} variant="caption" color="text.secondary">{shortDay.format(new Date(`${daily[i].date}T12:00:00Z`))}</Typography>)}
          </Box>
          </Box>
        </Box>
      </Box>
    </>
  );
}
