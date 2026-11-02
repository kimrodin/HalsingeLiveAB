export interface DashboardPeriod { from: string; to: string }

const dayMilliseconds = 24 * 60 * 60 * 1000;
const swedishDay = new Intl.DateTimeFormat('sv-SE', {
  timeZone: 'Europe/Stockholm', year: 'numeric', month: '2-digit', day: '2-digit',
});
const readableDay = new Intl.DateTimeFormat('sv-SE', {
  timeZone: 'Europe/Stockholm', day: 'numeric', month: 'short', year: 'numeric',
});

function validDay(value: string): boolean {
  if (!/^\d{4}-\d{2}-\d{2}$/.test(value)) return false;
  const date = new Date(`${value}T00:00:00Z`);
  return Number.isFinite(date.getTime()) && date.toISOString().slice(0, 10) === value;
}

export function shiftDate(value: string, days: number): string {
  const date = new Date(`${value}T00:00:00Z`);
  date.setUTCDate(date.getUTCDate() + days);
  return date.toISOString().slice(0, 10);
}

export function last30Days(now = new Date()): DashboardPeriod {
  const to = swedishDay.format(now);
  return { from: shiftDate(to, -29), to };
}

export function validatePeriod(period: DashboardPeriod): string | null {
  if (!validDay(period.from) || !validDay(period.to)) return 'Ange både ett giltigt från- och tilldatum.';
  if (period.from > period.to) return 'Fråndatum måste vara samma dag som eller före tilldatum.';
  if (period.from < '0002-01-01' || period.to > '9999-12-30') return 'Välj datum mellan år 2 och 9999-12-30.';
  const days = (Date.parse(`${period.to}T00:00:00Z`) - Date.parse(`${period.from}T00:00:00Z`)) / dayMilliseconds + 1;
  if (days > 366) return 'Välj en period på högst 366 dagar.';
  return null;
}

export function formatDate(value: string): string {
  return readableDay.format(new Date(`${value}T12:00:00Z`));
}
export function formatPeriod(period: DashboardPeriod): string {
  return period.from === period.to ? formatDate(period.from)
    : `${formatDate(period.from)} – ${formatDate(period.to)}`;
}

const swedishOffset = new Intl.DateTimeFormat('en', {
  timeZone: 'Europe/Stockholm', timeZoneName: 'longOffset',
});

function swedishMidnight(value: string): number {
  const midnight = Date.parse(`${value}T00:00:00Z`);
  let timestamp = midnight;
  // Resolve each boundary separately: Swedish days can span 23 or 25 hours.
  for (let i = 0; i < 3; i++) {
    const offset = swedishOffset.formatToParts(timestamp).find(part => part.type === 'timeZoneName')!.value;
    const match = /^GMT([+-])(\d{2}):(\d{2})(?::(\d{2}))?$/.exec(offset);
    const offsetSeconds = match
      ? (match[1] === '+' ? 1 : -1) * (Number(match[2]) * 3600 + Number(match[3]) * 60 + Number(match[4] ?? 0)) : 0;
    const resolved = midnight - offsetSeconds * 1000;
    if (resolved === timestamp) break;
    timestamp = resolved;
  }
  return timestamp;
}

export function eventPeriodBounds(period: DashboardPeriod): { startAfter: string; startsBefore: string } {
  return {
    startAfter: new Date(swedishMidnight(period.from)).toISOString(),
    startsBefore: new Date(swedishMidnight(shiftDate(period.to, 1)) - 1).toISOString(),
  };
}
