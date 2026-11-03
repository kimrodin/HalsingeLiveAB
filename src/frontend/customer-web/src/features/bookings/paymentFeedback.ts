export function paymentFeedback(bookingStatus: string, paymentStatus: string | null | undefined) {
  if (!['Cancelled', 'Expired', 'Failed'].includes(bookingStatus)) return null
  if (paymentStatus === 'Failed') {
    return {
      severity: 'error' as const,
      title: 'Betalningen misslyckades',
      message: 'Betalningen kunde inte genomföras och bokningen har avbokats. Gör en ny bokning för att försöka igen.',
    }
  }
  if (paymentStatus === 'TimedOut') {
    return {
      severity: 'warning' as const,
      title: 'Betalningstiden gick ut',
      message: 'Betalningen slutfördes inte i tid och bokningen har avbokats. Gör en ny bokning för att försöka igen.',
    }
  }
  return null
}
