import { lazy, Suspense } from 'react';
import { Box, Skeleton, Typography } from '@mui/material';
import { useTheme } from '@mui/material/styles';
import type { SalesStatistics } from '../api/salesStatistics';
import { formatDate } from '../utils/dashboardPeriod';

type Day = SalesStatistics['daily'][number];
const number = new Intl.NumberFormat('sv-SE');
const shortDay = new Intl.DateTimeFormat('sv-SE', { day: 'numeric', month: 'short', timeZone: 'Europe/Stockholm' });

function formatChartDay(value: string): string {
  return shortDay.formatToParts(new Date(`${value}T12:00:00Z`))
    .map(part => part.type === 'month' ? part.value.slice(0, 3) : part.value).join('');
}
const LineChart = lazy(() => import('@mui/x-charts/LineChart').then(module => ({ default: module.LineChart })));

export default function TicketSalesChart({ daily }: { daily: Day[] }) {
  const theme = useTheme();
  if (daily.length === 0) return <Typography color="text.secondary">Det finns inga dagsvärden för perioden.</Typography>;

  const total = daily.reduce((sum, day) => sum + day.soldTickets, 0);
  const peak = daily.reduce((best, day) => day.soldTickets > best.soldTickets ? day : best);

  return (
    <>
      {total === 0 && <Typography variant="body2" color="text.secondary">Ingen nettobiljettförsäljning under perioden.</Typography>}
      <Box tabIndex={0} role="region" aria-label="Biljettförsäljning, rulla i sidled för fler dagar" sx={{ minWidth: 0, overflowX: 'auto' }}>
        <Box sx={{ minWidth: Math.max(280, daily.length * 24 + 64) }}>
          <Suspense fallback={<Skeleton variant="rounded" height={290} />}>
            <LineChart
              height={290}
              title="Sålda biljetter per dag"
              desc={`${formatDate(daily[0].date)} till ${formatDate(daily[daily.length - 1].date)}. Totalt ${number.format(total)} biljetter efter återbetalningar. Högst ${number.format(peak.soldTickets)} biljetter på en dag.`}
              xAxis={[{
                scaleType: 'point',
                data: daily.map(day => day.date),
                tickInterval: () => true,
                tickLabelInterval: (_value, index) => index % 5 === 0,
                valueFormatter: (value: string, context) => context.location === 'tick'
                  ? formatChartDay(value)
                  : formatDate(value),
              }]}
              yAxis={[{
                min: 0,
                max: Math.max(4, Math.ceil(peak.soldTickets / 4) * 4),
                tickMinStep: 1,
                width: 64,
                valueFormatter: (value: number) => number.format(value),
              }]}
              series={[{
                id: 'soldTickets',
                data: daily.map(day => day.soldTickets),
                label: 'Sålda biljetter',
                color: theme.palette.primary.main,
                curve: 'linear',
                area: true,
                showMark: daily.length === 1,
                valueFormatter: value => value === null ? 'Saknas' : `${number.format(value)} biljetter`,
              }]}
              grid={{ vertical: true, horizontal: true }}
              sx={{ '& .MuiLineChart-area': { fillOpacity: 0.16 } }}
              hideLegend
              skipAnimation
            />
          </Suspense>
        </Box>
      </Box>
    </>
  );
}
