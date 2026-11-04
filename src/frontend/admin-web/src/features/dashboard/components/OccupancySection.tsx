import { lazy, Suspense } from 'react';
import { Alert, Box, Button, CircularProgress, Paper, Typography } from '@mui/material';
import { useTheme } from '@mui/material/styles';
import { useQuery } from '@tanstack/react-query';
import { ApiError } from '../../../shared/api/http';
import { getEventOccupancy } from '../api/occupancy';
import type { DashboardPeriod } from '../utils/dashboardPeriod';

const BarChart = lazy(() => import('@mui/x-charts/BarChart').then(({ BarChart }) => ({ default: BarChart })));
const date = new Intl.DateTimeFormat('sv-SE', {
  dateStyle: 'medium', timeStyle: 'short', timeZone: 'Europe/Stockholm',
});
const shortDate = new Intl.DateTimeFormat('sv-SE', { dateStyle: 'medium', timeZone: 'Europe/Stockholm' });
const time = new Intl.DateTimeFormat('sv-SE', { timeStyle: 'short', timeZone: 'Europe/Stockholm' });

function wrapEventName(name: string): string[] {
  const lines: string[] = [];
  const words = name.trim().split(/\s+/).flatMap(word => word.match(/.{1,20}/gu) ?? []);
  for (const word of words) {
    const last = lines.length - 1;
    if (last >= 0 && lines[last].length + word.length + 1 <= 20) lines[last] += ` ${word}`;
    else lines.push(word);
  }
  return lines;
}

const number = new Intl.NumberFormat('sv-SE', { maximumFractionDigits: 2 });

export default function OccupancySection({ period }: { period: DashboardPeriod }) {
  const theme = useTheme();
  const occupancy = useQuery({
    queryKey: ['dashboard', 'occupancy', period.from, period.to],
    queryFn: ({ signal }) => getEventOccupancy(period.from, period.to, signal),
    staleTime: 60_000,
    refetchInterval: 60_000,
    retry: (failures, error) => !(error instanceof ApiError && [400, 401, 403].includes(error.status)) && failures < 1,
  });
  const events = [...(occupancy.data ?? [])].sort((a, b) => b.occupancyPercent - a.occupancyPercent);
  const labels = new Map(events.map(event => [event.eventId,
    `${event.eventName} · ${date.format(new Date(event.eventStartsAt))}`]));
  const tickLabels = new Map(events.map(event => {
    const startsAt = new Date(event.eventStartsAt);
    return [event.eventId, [
      ...wrapEventName(event.eventName), shortDate.format(startsAt), `kl. ${time.format(startsAt)}`,
    ].join('\n')];
  }));
  const axisHeight = Math.max(3, ...Array.from(tickLabels.values(), label => label.split('\n').length)) * 18 + 20;

  return (
    <Paper sx={{ p: { xs: 2, sm: 3 }, mb: 3, minWidth: 0 }}>
      <Typography component="h2" variant="h6" sx={{ mb: 2 }}>Beläggning per evenemang</Typography>
      {occupancy.isError && <Alert severity={occupancy.data ? 'warning' : 'error'} sx={{ mb: 2 }}
        action={<Button color="inherit" disabled={occupancy.isFetching} onClick={() => void occupancy.refetch()}>Försök igen</Button>}>
        Kunde inte hämta beläggning.{occupancy.data ? ' Senast hämtade värden för perioden visas.' : ''}
      </Alert>}
      {occupancy.isPending ? <CircularProgress size={28} aria-label="Hämtar beläggning" />
        : occupancy.data && (events.length === 0
          ? <Typography color="text.secondary">Inga evenemang med biljettkapacitet hittades under perioden.</Typography>
          : <Suspense fallback={<CircularProgress size={28} aria-label="Laddar beläggningsdiagram" />}>
            <Box tabIndex={0} role="region" aria-label="Beläggning per evenemang, rulla i sidled för fler evenemang" sx={{ overflowX: 'auto' }}>
              <Box sx={{ minWidth: Math.max(320, events.length * 200 + 80) }}>
                <BarChart
                  height={270 + axisHeight}
                  title="Beläggning per evenemang"
                  desc={`${events.length} evenemang. Perioden avser startdatum i svensk tid.`}
                  colors={[theme.palette.primary.main]}
                  xAxis={[{
                    scaleType: 'band',
                    data: events.map(event => event.eventId),
                    valueFormatter: (id: string, context) => (context.location === 'tick' ? tickLabels : labels).get(id) ?? id,
                    height: axisHeight,
                    tickLabelInterval: () => true,
                    tickLabelStyle: { angle: 0, textAnchor: 'middle', dominantBaseline: 'hanging', fontSize: 12, lineHeight: '18px' },
                  }]}
                  yAxis={[{ min: 0, max: 110, width: 60, valueFormatter: (value: number) => `${number.format(value)} %` }]}
                  series={[{
                    data: events.map(event => event.occupancyPercent),
                    label: 'Beläggning',
                    barLabel: item => item.value === null ? null : `${number.format(item.value)} %`,
                    barLabelPlacement: 'outside',
                    valueFormatter: (value, { dataIndex }) => {
                      const event = events[dataIndex];
                      return value === null ? 'Saknas'
                        : `${number.format(value)} % · ${number.format(event.sold)} / ${number.format(event.capacity)} sålda · ${event.venueName}`;
                    },
                  }]}
                  grid={{ horizontal: true }}
                  hideLegend
                />
              </Box>
            </Box>
          </Suspense>)}
    </Paper>
  );
}
