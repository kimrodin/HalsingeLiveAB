import { request } from '../../../shared/api/http';

export interface PaymentSimulationSettings {
  outcome: 'Succeeded' | 'Failed' | 'TimedOut' | 'Random';
  delayMs: number;
  timeoutMs: number;
}

const baseUrl = '/payment-api';
const path = '/api/admin/payment-simulation';

export function getPaymentSimulation(): Promise<PaymentSimulationSettings> {
  return request(path, {}, baseUrl);
}

export function updatePaymentSimulation(settings: PaymentSimulationSettings): Promise<PaymentSimulationSettings> {
  return request(path, {
    method: 'PUT',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify(settings),
  }, baseUrl);
}
