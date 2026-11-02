import { Paper, Skeleton, Typography } from '@mui/material';

interface SalesMetricCardProps {
  title: string;
  value?: string;
  previousValue?: string;
  change?: number | null;
  comparisonPeriod?: string;
  caption: string;
  pending: boolean;
}
const percent = new Intl.NumberFormat('sv-SE', { maximumFractionDigits: 2, signDisplay: 'exceptZero' });

export default function SalesMetricCard({ title, value, previousValue, change, comparisonPeriod, caption, pending }: SalesMetricCardProps) {
  return (
    <Paper sx={{ p: 3, minWidth: 0 }}>
      <Typography component="h2" variant="h6">{title}</Typography>
      {pending ? <Skeleton width="75%" height={58} aria-label={`Hämtar ${title.toLocaleLowerCase('sv-SE')}`} /> : (
        <Typography variant="h3" component="p" sx={{ color: 'primary.light', my: 1, overflowWrap: 'anywhere' }}>
          {value ?? '—'}
        </Typography>
      )}
      <Typography variant="body2" color="text.secondary">{caption}</Typography>
      {value !== undefined && !pending && (
        <>
          <Typography variant="body2" sx={{ mt: 1, color: change === null || change === undefined || change === 0 ? 'text.secondary' : change > 0 ? 'success.main' : 'error.main' }}>
            {change === null || change === undefined ? `Föregående period: ${previousValue}`
              : `${percent.format(change)} % jämfört med föregående period`}
          </Typography>
          <Typography variant="caption" color="text.secondary">{comparisonPeriod}</Typography>
        </>
      )}
    </Paper>
  );
}
