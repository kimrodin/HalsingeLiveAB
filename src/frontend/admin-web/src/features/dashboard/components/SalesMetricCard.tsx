import { Paper, Skeleton, Typography } from '@mui/material';

interface SalesMetricCardProps {
  title: string;
  value?: string;
  change?: number | null;
  comparisonPeriod?: string;
  pending: boolean;
}
const percent = new Intl.NumberFormat('sv-SE', { maximumFractionDigits: 2, signDisplay: 'exceptZero' });

export default function SalesMetricCard({ title, value, change, comparisonPeriod, pending }: SalesMetricCardProps) {
  return (
    <Paper sx={{ p: 3, minWidth: 0 }}>
      <Typography component="h2" variant="h6">{title}</Typography>
      {pending ? <Skeleton width="75%" height={58} aria-label={`Hämtar ${title.toLocaleLowerCase('sv-SE')}`} /> : (
        <Typography variant="h3" component="p" sx={{ color: 'primary.light', my: 1, overflowWrap: 'anywhere' }}>
          {value ?? '—'}
        </Typography>
      )}
      {value !== undefined && !pending && change !== null && change !== undefined && (
        <>
          <Typography variant="body2" aria-label={`${percent.format(change)} procent jämfört med ${comparisonPeriod ?? 'föregående period'}`}
            sx={{ mt: 1, color: change === 0 ? 'text.secondary' : change > 0 ? 'success.main' : 'error.main' }}>
            {percent.format(change)} %
          </Typography>
          {comparisonPeriod && <Typography variant="caption" component="p" color="text.secondary">
            {comparisonPeriod}
          </Typography>}
        </>
      )}
    </Paper>
  );
}
