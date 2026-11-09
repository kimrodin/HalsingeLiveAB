import type { ReactNode } from 'react';
import { useEffect } from 'react';
import { Alert, Box, Button, Paper, Skeleton, Typography } from '@mui/material';
import { useQuery, useQueryClient } from '@tanstack/react-query';
import { HubConnectionBuilder, LogLevel } from '@microsoft/signalr';
import keycloak from '../../../app/auth/keycloak';
import { ApiError } from '../../../shared/api/http';
import { getSalesStatistics } from '../api/salesStatistics';
import { formatPeriod } from '../utils/dashboardPeriod';
import type { DashboardPeriod } from '../utils/dashboardPeriod';
import SalesMetricCard from './SalesMetricCard';
import TicketSalesChart from './TicketSalesChart';

const money = new Intl.NumberFormat('sv-SE', { style: 'currency', currency: 'SEK', minimumFractionDigits: 0, maximumFractionDigits: 2 });
const number = new Intl.NumberFormat('sv-SE');
export default function SalesStatisticsSection({ period, children }: { period: DashboardPeriod; children: ReactNode }) {
  const queryClient = useQueryClient();

  useEffect(() => {
    let active = true;
    let starting = false;
    let retryTimer: ReturnType<typeof setTimeout> | undefined;
    const connection = new HubConnectionBuilder()
      .withUrl('/booking-api/hubs/sales', {
        accessTokenFactory: async () => {
          await keycloak.updateToken(30);
          return keycloak.token ?? '';
        },
      })
      .withAutomaticReconnect()
      .configureLogging(LogLevel.Warning)
      .build();

    const refresh = () => void queryClient.invalidateQueries({ queryKey: ['sales-statistics'] });
    connection.on('SalesUpdated', refresh);
    connection.onreconnected(refresh);

    const connect = async () => {
      starting = true;
      try {
        await connection.start();
        if (!active) await connection.stop();
      } catch (error) {
        if (active) {
          console.warn('Sales updates connection failed to start; retrying.', error);
          retryTimer = setTimeout(() => void connect(), 5000);
        }
      } finally {
        starting = false;
      }
    };
    void connect();

    return () => {
      active = false;
      if (retryTimer) clearTimeout(retryTimer);
      connection.off('SalesUpdated', refresh);
      if (!starting) void connection.stop();
    };
  }, [queryClient]);

  const statistics = useQuery({
    queryKey: ['sales-statistics', period.from, period.to],
    queryFn: ({ signal }) => getSalesStatistics(period, signal),
    staleTime: 60_000,
    refetchInterval: 60_000,
    retry: (failures, error) => !(error instanceof ApiError && [400, 401, 403].includes(error.status)) && failures < 1,
  });
  const data = statistics.data;
  const comparisonPeriod = data ? formatPeriod(data.previousPeriod) : undefined;
  const displayedRange = data ? { from: data.from, to: data.to } : period;
  const errorMessage = statistics.error instanceof ApiError && [401, 403].includes(statistics.error.status)
    ? 'Statistiken kräver en giltig administratörsinloggning. Logga in igen om sessionen har gått ut.'
    : 'Försäljningsstatistiken kunde inte hämtas. Försök igen om en stund.';

  return (
    <>
      <Box role="status" aria-live="polite" sx={{ mb: 2 }}>
        <Typography variant="body2" color="text.secondary">
          Visar försäljning {formatPeriod(displayedRange)}{statistics.isFetching ? ' · Uppdaterar…' : ''}
        </Typography>
      </Box>
      {statistics.isError && <Alert severity={data ? 'warning' : 'error'} sx={{ mb: 2 }}
        action={<Button color="inherit" disabled={statistics.isFetching} onClick={() => void statistics.refetch()}>Försök igen</Button>}>
        {errorMessage}{data ? ' Senast hämtade värden för perioden visas.' : ''}
      </Alert>}

      <Box sx={{ display: 'grid', gridTemplateColumns: { xs: '1fr', lg: 'repeat(3, minmax(0, 1fr))' }, gap: 2, mb: 3 }}>
        <SalesMetricCard title="Sålda biljetter" pending={statistics.isPending}
          value={data && number.format(data.summary.soldTickets)}
          change={data?.changes.soldTicketsPercent} comparisonPeriod={comparisonPeriod} />
        <SalesMetricCard title="Omsättning" pending={statistics.isPending}
          value={data && money.format(data.summary.netRevenue)}
          change={data?.changes.netRevenuePercent} comparisonPeriod={comparisonPeriod} />
        {children}
      </Box>

      <Paper sx={{ p: { xs: 2, sm: 3 }, mb: 3 }}>
        <Typography component="h2" variant="h6" sx={{ mb: 2 }}>Biljettförsäljning</Typography>
        {statistics.isPending ? <Skeleton variant="rounded" height={290} aria-label="Hämtar biljettförsäljning" />
          : data ? <TicketSalesChart daily={data.daily} />
            : <Typography color="text.secondary">Grafen visas när försäljningsstatistiken har hämtats.</Typography>}
      </Paper>
    </>
  );
}
