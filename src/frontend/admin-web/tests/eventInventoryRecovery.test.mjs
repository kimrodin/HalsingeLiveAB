import assert from 'node:assert/strict';
import { readFile } from 'node:fs/promises';
import test from 'node:test';
import vm from 'node:vm';
import ts from 'typescript';
import * as jsxRuntime from 'react/jsx-runtime';

// Exercise the production modules with isolated HTTP/router/query dependencies.
// No backend data, browser login or additional testing packages are needed.
async function loadModule(relativePath, dependencies) {
  const source = await readFile(new URL(relativePath, import.meta.url), 'utf8');
  const { outputText } = ts.transpileModule(source, {
    compilerOptions: { module: ts.ModuleKind.ESNext, target: ts.ScriptTarget.ES2022, jsx: ts.JsxEmit.ReactJSX },
  });
  const context = vm.createContext({ URLSearchParams, console });
  const module = new vm.SourceTextModule(outputText, { context });
  await module.link(specifier => {
    const exports = dependencies[specifier];
    assert.ok(exports, `Unexpected dependency: ${specifier}`);
    return new vm.SyntheticModule(Object.keys(exports), function () {
      for (const [name, value] of Object.entries(exports)) this.setExport(name, value);
    }, { context });
  });
  await module.evaluate();
  return module.namespace;
}

class ApiError extends Error {
  constructor(status, message = 'API failure') { super(message); this.status = status; }
}
const event = {
  eventId: 'saved-event', venueId: 'venue-1', name: 'Konsert', description: 'Beskrivning',
  startDate: '2027-01-01T18:00:00Z', endDate: '2027-01-01T20:00:00Z', onSaleFrom: '2026-12-01T10:00:00Z',
  sectionPrices: [{ sectionId: 'a', price: 100 }, { sectionId: 'b', price: 200 }],
};
const rows = event.sectionPrices.map(section => ({ eventId: event.eventId, sectionId: section.sectionId }));
const apiPath = '../src/features/events/api/events.ts';
const httpPath = '../../../shared/api/http';
function loadApi(request, sendJson = () => { throw new Error('Unexpected create/update'); }) {
  return loadModule(apiPath, { [httpPath]: { ApiError, BASE_URL: '/api', request, sendJson } });
}

test('creation preserves the partial-success flag returned by the server', async () => {
  const response = { eventId: event.eventId, inventoryProvisioned: false };
  const api = await loadApi(() => {}, async () => response);
  assert.equal(await api.createEvent({ ...event }), response);
});

test('retry provisions the saved event through Booking without creating another event', async () => {
  const calls = [];
  const api = await loadApi(async (...args) => { calls.push(args); return rows; });
  assert.equal(await api.retryEventInventory(event.eventId), rows);
  assert.equal(calls.length, 1);
  assert.equal(calls[0][0], '/CreateEventInventoryFromCatalog/from-catalog/saved-event');
  assert.equal(calls[0][1].method, 'POST');
  assert.equal(calls[0][2], '/booking-api');
});

test('a lost response followed by 409 is recovered only after checking every section', async () => {
  const calls = [];
  const api = await loadApi(async path => {
    calls.push(path);
    if (path.startsWith('/CreateEventInventory')) throw new ApiError(409);
    return path.startsWith('/GetEvent/') ? event : rows;
  });
  assert.equal(await api.retryEventInventory(event.eventId), rows);
  assert.equal(calls.length, 3);
  assert.ok(calls.includes('/GetEvent/saved-event'));
  assert.ok(calls.includes('/ListEventInventory/saved-event'));
});

test('409 with partial inventory is not reported as success', async () => {
  const api = await loadApi(async path => {
    if (path.startsWith('/CreateEventInventory')) throw new ApiError(409);
    return path.startsWith('/GetEvent/') ? event : rows.slice(0, 1);
  });
  await assert.rejects(api.retryEventInventory(event.eventId), /ofullständigt/);
  assert.equal(api.hasCompleteEventInventory(event, rows.map(row => ({ ...row, eventId: 'other-event' }))), false);
  assert.equal(api.hasCompleteEventInventory({ ...event, sectionPrices: [] }, []), false);
});

for (const status of [401, 403, 503]) {
  test(`retry preserves HTTP ${status} so the UI can show the failure`, async () => {
    const failure = new ApiError(status);
    let calls = 0;
    const api = await loadApi(async () => { calls++; throw failure; });
    await assert.rejects(api.retryEventInventory(event.eventId), error => error === failure);
    assert.equal(calls, 1);
  });
}

const mui = Object.fromEntries([
  'Alert', 'Box', 'Button', 'CircularProgress', 'MenuItem', 'Paper', 'Stack', 'TextField', 'Typography',
].map(name => [name, name]));
function elements(node) {
  if (!node || typeof node !== 'object') return [];
  if (Array.isArray(node)) return node.flatMap(elements);
  return [node, ...elements(node.props?.children)];
}
function text(node) {
  if (typeof node === 'string') return node;
  if (Array.isArray(node)) return node.map(text).join(' ');
  return node?.props ? text(node.props.children) : '';
}

async function formHarness(id) {
  const navigation = [];
  let mutation;
  const writes = [];
  const page = await loadModule('../src/features/events/pages/EventFormPage.tsx', {
    react: { useState: value => [value, () => {}] },
    'react/jsx-runtime': jsxRuntime,
    'react-router': {
      useNavigate: () => (...args) => navigation.push(args), useParams: () => ({ id }),
      useSearchParams: () => [new URLSearchParams()],
    },
    '@mui/material': mui,
    '@tanstack/react-query': {
      useQuery: options => ({ data: options.queryKey[0] === 'venues' ? [] : id ? event : undefined }),
      useMutation: options => { mutation = options; return {}; },
      useQueryClient: () => ({ invalidateQueries: async () => {} }),
    },
    '../api/events': {
      createEvent: async data => { writes.push(['create', data]); },
      updateEvent: async (...args) => { writes.push(['update', ...args]); }, getEvent: async () => event,
    },
    '../../venues/api/venues': { getVenues: async () => [] },
    '../components/EventImageManager': { default: 'EventImageManager' },
    '../components/EventInventoryStatus': { default: 'EventInventoryStatus' },
    '../../seating-maps/components/VenueSeatingMapViewer': { default: 'VenueSeatingMapViewer' },
  });
  const form = elements(page.default()).find(element => element.type?.name === 'EventForm');
  form.type(form.props);
  return { mutation, navigation, writes };
}

test('partial creation replaces the new-event route with the saved event and recovery notice', async () => {
  const { mutation, navigation } = await formHarness();
  await mutation.onSuccess({ eventId: event.eventId, inventoryProvisioned: false });
  assert.equal(navigation[0][0], '/events/saved-event?inventoryPending=1');
  assert.equal(navigation[0][1].replace, true);
});

test('successful creation and normal updates still return to the list', async () => {
  const { mutation, navigation } = await formHarness();
  await mutation.onSuccess({ eventId: event.eventId, inventoryProvisioned: true });
  await mutation.onSuccess(undefined);
  assert.deepEqual(navigation, [['/events'], ['/events']]);
});

test('the saved-event route updates the original event instead of submitting CreateEvent again', async () => {
  const { mutation, writes } = await formHarness(event.eventId);
  await mutation.mutationFn({ ...event, prices: { a: '100', b: '200' } });
  assert.equal(writes.length, 1);
  assert.equal(writes[0][0], 'update');
  assert.equal(writes[0][1], event.eventId);
});

test('missing inventory stays visible after reloading the saved event and retry targets that ID', async () => {
  const api = await loadApi(() => {});
  const query = { data: [], isPending: false, isFetching: false, isError: false };
  const mutationState = {};
  let mutation;
  let retriedId;
  const component = await loadModule('../src/features/events/components/EventInventoryStatus.tsx', {
    'react/jsx-runtime': jsxRuntime, '@mui/material': mui,
    'react-router': { Link: 'RouterLink' },
    [httpPath]: { ApiError },
    '../api/events': {
      getEventInventory: async () => [], hasCompleteEventInventory: api.hasCompleteEventInventory,
      retryEventInventory: async id => { retriedId = id; return rows; },
    },
    '@tanstack/react-query': {
      useQuery: () => query,
      useMutation: options => { mutation = options; return mutationState; },
      useQueryClient: () => ({ setQueryData: (key, data) => {
        assert.equal(key[1], event.eventId); query.data = data;
      } }),
    },
  });
  const props = { event, initialFailure: false };
  assert.match(text(component.default(props)), /Evenemanget är sparat, men biljettlagret kunde inte skapas/);
  mutationState.isError = true;
  mutationState.error = new ApiError(401);
  const failedRetry = component.default(props);
  assert.match(text(failedRetry), /Kontrollera att du är inloggad som administratör/);
  assert.match(text(failedRetry), /Försök skapa biljettlagret igen/);
  mutationState.isError = false;
  const result = await mutation.mutationFn();
  mutation.onSuccess(result);
  mutationState.isSuccess = true;
  assert.equal(retriedId, event.eventId);
  assert.match(text(component.default(props)), /biljettlagret är klart/);
});
