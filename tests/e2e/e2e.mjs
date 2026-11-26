// End-to-end test of the SportX API, driven like real users would.
import { execFileSync } from 'node:child_process';
import { readdirSync } from 'node:fs';

const BASE = process.env.API_URL ?? 'http://localhost:5299';
const FUTSAL = '10000000-0000-0000-0000-000000000002';
const TENNIS = '10000000-0000-0000-0000-000000000004';
const PARKING = '20000000-0000-0000-0000-000000000001';
const SHOWERS = '20000000-0000-0000-0000-000000000003';
const PG = readdirSync('C:/Program Files/PostgreSQL').map(v => `C:/Program Files/PostgreSQL/${v}/bin/psql.exe`)[0];

const results = [];
let section = '';
const run = Date.now().toString(36);

function sql(query) {
  return execFileSync(PG, ['-h', 'localhost', '-U', 'postgres', '-d', 'FiveAndOne', '-t', '-A', '-c', query],
    { env: { ...process.env, PGPASSWORD: process.env.PGPASSWORD ?? 'bookrentalplatform' } }).toString().trim();
}

async function api(method, path, { token, body, headers = {}, form, business } = {}) {
  const h = { 'Accept-Language': 'en', ...headers };
  if (token) h.Authorization = `Bearer ${token}`;
  if (business) h['X-Business-Id'] = business;
  let payload;
  if (form) payload = form;
  else if (body !== undefined) { h['Content-Type'] = 'application/json'; payload = JSON.stringify(body); }
  const res = await fetch(BASE + path, { method, headers: h, body: payload, redirect: 'manual' });
  const text = await res.text();
  let data; try { data = text ? JSON.parse(text) : null; } catch { data = text; }
  return { status: res.status, data };
}

function check(name, ok, info) {
  results.push({ section, name, ok: !!ok, info: ok ? '' : info });
  if (!ok) console.log(`  FAIL [${section}] ${name} :: ${typeof info === 'string' ? info : JSON.stringify(info)?.slice(0, 400)}`);
}
function expect(res, status, name, extra = () => true) {
  const ok = res.status === status && extra(res.data);
  check(`${name} -> ${status}`, ok, { got: res.status, body: res.data });
  return res.data;
}
const code = (d) => d?.code;

// Kosovo local date helpers
const localDate = (daysAhead) => {
  const d = new Date(Date.now() + daysAhead * 86400000);
  return new Intl.DateTimeFormat('en-CA', { timeZone: 'Europe/Belgrade', year: 'numeric', month: '2-digit', day: '2-digit' }).format(d);
};
const D2 = localDate(2), D3 = localDate(3), D4 = localDate(4), D5 = localDate(5), D6 = localDate(6), D8 = localDate(8), D9 = localDate(9);
const slotAt = (day, hhmm) => day.slots.find(s => s.localStartsAt.slice(11, 16) === hhmm);

async function login(email, password = 'Password1!') {
  const r = await api('POST', '/api/auth/login', { body: { email, password } });
  return r.data?.accessToken;
}
async function registerCustomer(tag, extra = {}) {
  const email = `e2e_${tag}_${run}@example.com`;
  const r = await api('POST', '/api/auth/register', { body: { firstName: tag, lastName: 'Tester', username: `e2e_${tag}_${run}`, email, password: 'Password1!', ...extra } });
  return { email, username: `e2e_${tag}_${run}`, token: r.data?.accessToken, id: r.data?.userId, res: r };
}

const PNG = () => { const b = new Uint8Array(200); b.set([0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A]); return new Blob([b], { type: 'image/png' }); };

async function main() {
  // ================================================================= Public catalogue
  section = 'catalogue';
  let r = await api('GET', '/api/sports', { headers: { 'Accept-Language': 'sq' } });
  expect(r, 200, 'sports (sq)', d => d.some(s => s.name === 'Futsall'));
  expect(await api('GET', '/api/sports?includeCounts=true'), 200, 'sports with counts', d => d.every(s => s.facilityCount !== null));
  expect(await api('GET', '/api/sports/futsal'), 200, 'sport by slug');
  expect(await api('GET', '/api/sports/nope'), 404, 'unknown sport');
  expect(await api('GET', '/api/amenities', { headers: { 'Accept-Language': 'tr' } }), 200, 'amenities (tr)', d => d.some(a => a.name === 'Otopark'));
  expect(await api('GET', '/api/cities'), 200, 'cities');
  expect(await api('GET', '/api/feature-flags'), 200, 'feature flags', d => d.guest_booking === true);
  expect(await api('GET', '/api/plans'), 200, 'plans');
  expect(await api('GET', '/api/home'), 200, 'home', d => Array.isArray(d.sports) && Array.isArray(d.cities));
  expect(await api('GET', '/openapi/v1.json'), 200, 'openapi documents X-Business-Id', d => JSON.stringify(d).includes('X-Business-Id'));
  expect(await api('GET', '/health'), 200, 'health check', d => d === 'Healthy');
  expect(await api('GET', '/api/does-not-exist'), 404, 'unknown route uses ErrorResponse', d => d.code === 'not_found');

  // ================================================================= Auth
  section = 'auth';
  const c1 = await registerCustomer('cust1', { phoneNumber: '+38349100001', preferredLanguage: 'en' });
  expect(c1.res, 200, 'register customer');
  expect(await api('POST', '/api/auth/register', { body: { firstName: 'X', username: c1.username, email: `other_${run}@example.com`, password: 'Password1!' } }), 409, 'duplicate username');
  expect(await api('POST', '/api/auth/register', { body: { firstName: 'X', username: 'ab', email: 'bad', password: 'short' } }), 400, 'invalid registration', d => d.errors);
  r = await api('POST', '/api/auth/login', { body: { email: c1.email, password: 'Password1!' } });
  expect(r, 200, 'login');
  const c1Refresh = r.data.refreshToken;
  c1.token = r.data.accessToken;
  expect(await api('GET', '/api/auth/me', { token: c1.token }), 200, 'me');
  expect(await api('GET', '/api/auth/me'), 401, 'me anonymous', d => d.code === 'unauthorized');
  r = await api('POST', '/api/auth/refresh', { body: { refreshToken: c1Refresh } });
  expect(r, 200, 'refresh');
  expect(await api('POST', '/api/auth/refresh', { body: { refreshToken: c1Refresh } }), 401, 'refresh token reuse rejected');
  c1.token = await login(c1.email);

  const lock = await registerCustomer('lock');
  for (let i = 0; i < 5; i++) await api('POST', '/api/auth/login', { body: { email: lock.email, password: 'WrongPass1!' } });
  expect(await api('POST', '/api/auth/login', { body: { email: lock.email, password: 'Password1!' } }), 429, 'lockout after 5 failures', d => d.code === 'account_locked');

  // Business registration (story 06)
  const ownerEmail = `e2e_owner_${run}@example.com`;
  const bizBody = {
    businessName: `E2E Arena ${run}`, registrationNumber: `81${String(Date.now()).slice(-7)}`, ownerFirstName: 'Owner', ownerLastName: 'One',
    email: ownerEmail, phone: '+38344111222', password: 'Password1!', addressLine: 'Rr. Agim Ramadani 1', city: 'Prishtinë', country: 'XK', preferredLanguage: 'sq'
  };
  r = await api('POST', '/api/auth/register-business', { body: bizBody });
  expect(r, 200, 'register business', d => d.role === 'Business');
  let owner = r.data.accessToken;
  expect(await api('POST', '/api/auth/register-business', { body: { ...bizBody, email: `e2e_dup_${run}@example.com` } }), 409, 'duplicate registration number', d => d.code === 'business_already_registered');
  expect(await api('POST', '/api/auth/register-business', { body: { ...bizBody, email: `e2e_bad_${run}@example.com`, registrationNumber: '12AB' } }), 400, 'invalid Kosovo registration number');

  // A second business owner (data isolation, multiple businesses, retention)
  r = await api('POST', '/api/auth/register-business', { body: { ...bizBody, businessName: `E2E Other ${run}`, registrationNumber: `82${String(Date.now()).slice(-7)}`, email: `e2e_owner2_${run}@example.com` } });
  expect(r, 200, 'register second business');
  const owner2 = r.data.accessToken;

  // ================================================================= Admin setup
  section = 'admin-setup';
  const admin = await login(process.env.SUPERADMIN_EMAIL ?? 'superadmin@fiveandone.local', process.env.SUPERADMIN_PASSWORD ?? 'SuperAdmin123!');
  check('superadmin login', !!admin, 'no token');
  r = await api('POST', '/api/admin/plans', { token: admin, body: { name: `E2E Pro ${run}`, description: 'E2E plan', price: 19.99, billingInterval: 'Monthly', maxFacilities: 3, isActive: true } });
  const plan = expect(r, 201, 'create plan');
  expect(await api('PUT', `/api/admin/plans/${plan.id}`, { token: admin, body: { name: plan.name, price: 19.99, maxFacilities: 3, isActive: true, sortOrder: 1 } }), 200, 'update plan');
  expect(await api('POST', '/api/admin/plans', { token: c1.token, body: { name: 'x', price: 1 } }), 403, 'customer cannot create plans');
  expect(await api('GET', '/api/admin/settings', { token: admin }), 200, 'settings');
  expect(await api('PUT', '/api/admin/settings/guest.max_attempts_per_hour', { token: admin, body: { value: 'abc' } }), 400, 'invalid setting value');
  expect(await api('PUT', '/api/admin/settings/guest.max_attempts_per_hour', { token: admin, body: { value: '1000' } }), 200, 'raise guest attempts for test');
  expect(await api('PUT', '/api/admin/settings/nope', { token: admin, body: { value: '1' } }), 404, 'unknown setting');

  // ================================================================= Business profile & subscription
  section = 'business';
  expect(await api('GET', '/api/business/mine', { token: owner }), 200, 'my businesses', d => d.length === 1 && d[0].role === 'Owner');
  r = await api('GET', '/api/business/profile', { token: owner });
  expect(r, 200, 'get profile', d => d.businessName === bizBody.businessName && d.hasRegistrationNumber && !('registrationNumber' in d));
  const businessId = r.data.id;
  expect(await api('PUT', '/api/business/profile', { token: owner, body: { businessName: `E2E Arena ${run}`, addressLine: 'Rr. Agim Ramadani 2', city: 'Prishtinë', country: 'xk', phone: '+383 44 111 222', email: 'Owner@Example.com', preferredLanguage: 'EN' } }), 200, 'update profile', d => d.country === 'XK' && d.email === 'owner@example.com');
  expect(await api('PUT', '/api/business/profile', { token: owner, body: { businessName: '' } }), 400, 'profile validation');
  expect(await api('PUT', '/api/business/cancellation-policy', { token: owner, body: { cancellationCutoffHours: 24, cancellationPolicyNote: 'Free cancellation up to 24h before.' } }), 200, 'cancellation policy');
  expect(await api('GET', '/api/business/profile', { token: c1.token }), 403, 'customer has no business profile');
  expect(await api('GET', '/api/business/subscription', { token: owner }), 200, 'subscription (none yet)', d => d.status === null && d.notice);
  expect(await api('GET', '/api/business/facilities/usage', { token: owner }), 200, 'usage without subscription', d => !d.hasActiveSubscription && d.maxFacilities === 1);

  r = await api('POST', '/api/business/subscription/checkout', { token: owner, body: { planId: plan.id } });
  const checkout = expect(r, 200, 'checkout', d => d.checkoutUrl?.includes('/api/payments/mock/'));
  expect(await api('GET', `/api/payments/mock/${checkout.paymentId}`), 200, 'mock checkout page');
  expect(await api('GET', '/api/business/subscription', { token: owner }), 200, 'still not active before webhook', d => d.status === null);
  expect(await api('POST', `/api/payments/mock/${checkout.paymentId}/complete?succeeded=false`), 204, 'mock payment fails');
  expect(await api('GET', '/api/business/subscription', { token: owner }), 200, 'failed payment does not activate', d => d.status === null && d.payments.some(p => p.status === 'Failed'));
  r = await api('POST', '/api/business/subscription/checkout', { token: owner, body: { planId: plan.id } });
  expect(await api('POST', `/api/payments/mock/${r.data.paymentId}/complete?succeeded=true`), 204, 'mock payment succeeds');
  r = await api('GET', '/api/business/subscription', { token: owner });
  expect(r, 200, 'subscription active', d => d.status === 'Active' && d.plan.name === plan.name && d.nextBillingDate);
  expect(await api('POST', '/api/business/subscription/checkout', { token: owner, body: { planId: plan.id } }), 409, 'already subscribed', d => d.code === 'already_subscribed');
  expect(await api('POST', '/api/payments/webhook', { body: { reference: 'x', succeeded: true } }), 401, 'webhook without signature rejected');
  expect(await api('POST', '/api/business/subscription/cancel', { token: owner }), 200, 'cancel at period end', d => d.cancelAtPeriodEnd);
  expect(await api('POST', '/api/business/subscription/resume', { token: owner }), 200, 'resume', d => !d.cancelAtPeriodEnd);
  expect(await api('POST', '/api/business/subscription/pay', { token: owner }), 409, 'nothing to pay', d => d.code === 'nothing_to_pay');

  // ================================================================= Facilities (08-12)
  section = 'facilities';
  r = await api('POST', '/api/business/facilities', { token: owner, body: { name: 'Fusha Futsall 1' } });
  const fac = expect(r, 201, 'create draft', d => d.status === 'Draft' && d.missingForPublish.operatingHours && d.missingForPublish.pricing);
  const F = fac.id;
  expect(await api('GET', `/api/business/facilities/${F}`, { token: owner }), 200, 'get facility');
  expect(await api('GET', '/api/business/facilities', { token: owner }), 200, 'list facilities', d => d.length === 1);
  expect(await api('GET', '/api/business/facilities?status=Published', { token: owner }), 200, 'filter by status', d => d.length === 0);
  expect(await api('POST', `/api/business/facilities/${F}/publish`, { token: owner }), 400, 'publish incomplete', d => d.code === 'facility_incomplete');
  expect(await api('POST', `/api/business/facilities/${F}/hide`, { token: owner }), 409, 'hide draft');
  expect(await api('GET', `/api/business/facilities/${F}`, { token: owner2 }), 404, 'other business gets 404');
  expect(await api('POST', '/api/business/facilities', { token: owner, body: { name: '' } }), 400, 'name required');
  expect(await api('POST', '/api/business/facilities', { token: owner, body: { name: 'X', sportIds: ['10000000-0000-0000-0000-000000000099'] } }), 400, 'unknown sport rejected');

  r = await api('PUT', `/api/business/facilities/${F}`, { token: owner, body: {
    name: 'Fusha Futsall 1', description: 'Indoor futsal pitch', type: 'Pitch', capacity: 12, isIndoor: true, surface: 'Artificial grass',
    sportIds: [FUTSAL, TENNIS], addressLine: 'Rr. Agim Ramadani 2', city: 'Prishtinë', country: 'XK', latitude: 42.6629, longitude: 21.1655,
    amenityIds: [PARKING, SHOWERS], imageUrls: ['https://example.com/pitch.jpg'] } });
  expect(r, 200, 'update facility', d => d.sports.length === 2 && d.amenities.length === 2 && d.images.length === 1);

  // Sports with own rules (09)
  expect(await api('PUT', `/api/business/facilities/${F}/sports`, { token: owner, body: { sports: [{ sportId: FUTSAL, durationOptions: [60, 90] }, { sportId: TENNIS, durationOptions: [60], bufferMinutes: 0 }] } }), 200, 'save sports', d => d.sports.find(s => s.sportId === FUTSAL).effectiveDurationOptions.join() === '60,90');
  expect(await api('PUT', `/api/business/facilities/${F}/sports`, { token: owner, body: { sports: [{ sportId: FUTSAL, durationOptions: [7] }] } }), 400, 'invalid sport duration');

  // Rules (16, 33)
  expect(await api('PUT', `/api/business/facilities/${F}/rules`, { token: owner, body: { durationOptions: [60, 90, 120], bufferMinutes: 10, slotStepMinutes: 30, minAdvanceMinutes: 0, maxAdvanceDays: 30, cancellationCutoffHours: 24 } }), 200, 'save rules', d => d.bufferMinutes === 10);
  expect(await api('PUT', `/api/business/facilities/${F}/rules`, { token: owner, body: { durationOptions: [60], slotStepMinutes: 7 } }), 400, 'invalid slot step');

  // Weekly hours (14)
  const week = ['Monday', 'Tuesday', 'Wednesday', 'Thursday', 'Friday', 'Saturday', 'Sunday'].map(d => ({ dayOfWeek: d, windows: [{ opensAt: '08:00', closesAt: '23:00' }] }));
  expect(await api('PUT', `/api/business/facilities/${F}/hours`, { token: owner, body: { days: week } }), 200, 'save weekly hours', d => d.summary === 'Mon–Sun 08:00–23:00');
  expect(await api('PUT', `/api/business/facilities/${F}/hours`, { token: owner, body: { days: [{ dayOfWeek: 'Monday', windows: [{ opensAt: '08:00', closesAt: '12:00' }, { opensAt: '11:00', closesAt: '14:00' }] }] } }), 400, 'overlapping windows');
  expect(await api('PUT', `/api/business/facilities/${F}/hours`, { token: owner, body: { days: [{ dayOfWeek: 'Friday', windows: [{ opensAt: '18:00', closesAt: '09:00' }] }, { dayOfWeek: 'Saturday', windows: [{ opensAt: '08:00', closesAt: '20:00' }] }] } }), 400, 'overnight running into next day');
  expect(await api('PUT', `/api/business/facilities/${F}/hours`, { token: owner, body: { sportId: TENNIS, days: [{ dayOfWeek: 'Monday', windows: [{ opensAt: '09:00', closesAt: '12:00' }] }] } }), 200, 'sport-specific hours');
  expect(await api('GET', `/api/business/facilities/${F}/hours?sportId=${TENNIS}`, { token: owner }), 200, 'get sport hours', d => d.days.find(x => x.dayOfWeek === 'Monday').windows.length === 1);
  expect(await api('DELETE', `/api/business/facilities/${F}/hours/${TENNIS}`, { token: owner }), 204, 'delete sport hours');
  expect(await api('GET', `/api/business/facilities/${F}/hours`, { token: owner }), 200, 'get facility hours');

  // Pricing (13)
  r = await api('PUT', `/api/business/facilities/${F}/pricing`, { token: owner, body: { rules: [
    { amount: 25, priceType: 'PerReservation', label: 'Default' },
    { sportId: FUTSAL, durationMinutes: 90, amount: 35, label: '90 min' },
    { sportId: FUTSAL, startTime: '18:00', endTime: '23:00', amount: 40, label: 'Evening', priority: 1 },
    { sportId: TENNIS, priceType: 'PerHour', amount: 12 } ] } });
  expect(r, 200, 'save pricing', d => d.length === 4);
  expect(await api('PUT', `/api/business/facilities/${F}/pricing`, { token: owner, body: { rules: [{ amount: 10, currency: 'USD' }] } }), 400, 'only EUR');
  expect(await api('GET', `/api/business/facilities/${F}/pricing/preview?sportId=${FUTSAL}&date=${D2}&startTime=19:00&durationMinutes=60`, { token: owner }), 200, 'price preview evening', d => d.price === 40);
  expect(await api('GET', `/api/business/facilities/${F}/pricing/preview?sportId=${TENNIS}&date=${D2}&startTime=10:00&durationMinutes=90`, { token: owner }), 200, 'price preview per hour', d => d.price === 18);
  expect(await api('GET', `/api/business/facilities/${F}/pricing`, { token: owner }), 200, 'get pricing');

  // Images (10)
  let fd = new FormData(); fd.append('files', PNG(), 'a.png'); fd.append('files', PNG(), 'b.png');
  r = await api('POST', `/api/business/facilities/${F}/images`, { token: owner, form: fd });
  expect(r, 200, 'upload images', d => d.length === 3 && d.some(i => i.url.startsWith('/uploads/')));
  const uploaded = r.data.find(i => i.url.startsWith('/uploads/'));
  expect(await api('GET', uploaded.url), 200, 'uploaded image is served');
  fd = new FormData(); fd.append('files', new Blob(['not an image at all, just text'], { type: 'image/png' }), 'fake.png');
  expect(await api('POST', `/api/business/facilities/${F}/images`, { token: owner, form: fd }), 400, 'fake image rejected', d => d.code === 'invalid_image');
  const order = r.data.map(i => i.id).reverse();
  expect(await api('PUT', `/api/business/facilities/${F}/images/order`, { token: owner, body: order }), 200, 'reorder images', d => d[0].id === order[0] && d[0].isCover);
  expect(await api('DELETE', `/api/business/facilities/${F}/images/${order[0]}`, { token: owner }), 200, 'delete image', d => d.length === 2 && d[0].isCover);

  // Configuration + publish (12)
  expect(await api('GET', `/api/business/facilities/${F}/configuration`, { token: owner }), 200, 'configuration', d => d.priceRules.length === 4 && d.facilityHours.days.length === 7);
  expect(await api('POST', `/api/business/facilities/${F}/publish`, { token: owner }), 200, 'publish', d => d.status === 'Published');
  expect(await api('PUT', `/api/business/facilities/${F}`, { token: owner, body: { name: 'Fusha Futsall 1', sportIds: [], imageUrls: null } }), 400, 'published must stay complete');
  expect(await api('POST', `/api/business/facilities/${F}/hide`, { token: owner }), 200, 'hide', d => d.status === 'Hidden');
  expect(await api('GET', `/api/facilities/${F}`), 404, 'hidden not public');
  expect(await api('POST', `/api/business/facilities/${F}/publish`, { token: owner }), 200, 're-publish');
  expect(await api('GET', '/api/business/facilities/usage', { token: owner }), 200, 'usage with plan', d => d.hasActiveSubscription && d.maxFacilities === 3 && d.publishedFacilities === 1);

  // Plan limit
  const extra = [];
  for (let i = 0; i < 2; i++) extra.push((await api('POST', '/api/business/facilities', { token: owner, body: { name: `Extra ${i}` } })).data.id);
  expect(await api('POST', '/api/business/facilities', { token: owner, body: { name: 'Over limit' } }), 403, 'plan facility limit', d => d.code === 'plan_limit_reached');
  fd = new FormData(); fd.append('files', PNG(), 'c.png');
  const extraImage = (await api('POST', `/api/business/facilities/${extra[1]}/images`, { token: owner, form: fd })).data?.[0]?.url;
  expect(await api('GET', extraImage), 200, 'image of facility to delete is served');
  expect(await api('DELETE', `/api/business/facilities/${extra[1]}`, { token: owner }), 204, 'delete unbooked facility');
  expect(await api('GET', extraImage), 404, 'uploaded file removed with the facility');
  const slug = (await api('GET', `/api/business/facilities/${F}`, { token: owner })).data.slug;

  // ================================================================= Public discovery (17-27)
  section = 'discovery';
  r = await api('GET', `/api/facilities/${slug}`, { headers: { 'Accept-Language': 'sq' } });
  expect(r, 200, 'details by slug', d => d.businessName && d.prices.length === 4 && d.cancellationCutoffHours === 24 && d.sports.some(s => s.name === 'Futsall') && d.operatingHours.summary);
  r = await api('GET', `/api/facilities/${F}/availability?sportId=${FUTSAL}&date=${D2}`);
  const day2 = expect(r, 200, 'availability (default 60)', d => d.durationMinutes === 60 && d.slots.length > 0 && d.slots.every(s => s.price));
  check('evening slot priced 40', slotAt(day2, '19:00')?.price === 40, slotAt(day2, '19:00'));
  check('day slot priced 25', slotAt(day2, '10:00')?.price === 25, slotAt(day2, '10:00'));
  expect(await api('GET', `/api/facilities/${F}/availability?sportId=${FUTSAL}&date=${D2}&durationMinutes=90`), 200, 'availability 90 min', d => d.slots.find(s => s.localStartsAt.endsWith('10:00:00'))?.price === 35);
  expect(await api('GET', `/api/facilities/${F}/availability?sportId=${FUTSAL}&date=${D2}&durationMinutes=45`), 400, 'invalid duration', d => d.code === 'invalid_duration');
  expect(await api('GET', `/api/facilities/${F}/availability?sportId=10000000-0000-0000-0000-000000000009&date=${D2}`), 404, 'sport not offered');
  expect(await api('GET', `/api/facilities/${F}/calendar?sportId=${FUTSAL}&from=${D2}&days=7`), 200, 'calendar', d => d.days.length === 7 && d.days[0].availableSlots > 0);
  expect(await api('GET', `/api/facilities?sport=futsal&city=Prishtinë&date=${D2}&time=19:00&timeFlexMinutes=60`), 200, 'search', d => d.items.some(c => c.id === F && c.nextSlots.length > 0));
  expect(await api('GET', `/api/facilities?search=Futsall&sort=price_asc`), 200, 'search text + price sort', d => d.items.some(c => c.id === F));
  expect(await api('GET', `/api/facilities?sort=distance&latitude=42.66&longitude=21.16&radiusKm=5`), 200, 'distance sort + radius', d => d.items.find(c => c.id === F)?.distanceKm < 5);
  expect(await api('GET', `/api/facilities?sort=distance`), 400, 'distance needs coordinates');
  expect(await api('GET', `/api/facilities?minPrice=100`), 200, 'price filter', d => !d.items.some(c => c.id === F));
  expect(await api('GET', `/api/facilities?sort=availability&availableOnly=true&date=${D2}`), 200, 'availability sort');
  expect(await api('GET', '/api/facilities/map?minLat=42&maxLat=43&minLng=20&maxLng=22&sport=futsal'), 200, 'map', d => d.some(m => m.id === F));
  expect(await api('GET', '/api/home'), 200, 'home has featured', d => d.featuredFacilities.some(c => c.id === F));
  expect(await api('GET', '/api/geocoding/search?q=Rr. Test, Prizren', { token: owner }), 200, 'geocoding', d => d[0]?.city === 'Prizren');
  expect(await api('GET', '/api/geocoding/search?q=x'), 401, 'geocoding needs login');
  expect(await api('GET', `/api/business/facilities/${F}/availability?sportId=${FUTSAL}&date=${D2}`, { token: owner }), 200, 'business slot preview');

  // ================================================================= Customer bookings (28-36)
  section = 'bookings';
  expect(await api('PUT', '/api/account/profile', { token: c1.token, body: { firstName: 'Cust', lastName: 'One', phoneNumber: '+38349100001', city: 'Prishtinë', preferredLanguage: 'en' } }), 200, 'save profile');
  expect(await api('GET', '/api/account/profile', { token: c1.token }), 200, 'get profile', d => d.city === 'Prishtinë');

  const book = (token, startsAt, extraBody = {}, headers = {}) => api('POST', '/api/reservations', { token, headers, body: { facilityId: F, sportId: FUTSAL, startsAt, durationMinutes: 60, ...extraBody } });
  let s10 = slotAt(day2, '10:00'), s12 = slotAt(day2, '12:00'), s14 = slotAt(day2, '14:00');
  r = await book(c1.token, s10.startsAt, { notes: 'Bring ball' }, { 'Idempotency-Key': `k1-${run}` });
  const b1 = expect(r, 201, 'customer booking', d => d.status === 'Confirmed' && d.price === 25 && d.bookingNumber.startsWith('SX-') && d.cancellationPolicy.canCancel && !d.guestAccessToken);
  expect(await book(c1.token, s10.startsAt, { notes: 'Bring ball' }, { 'Idempotency-Key': `k1-${run}` }), 201, 'idempotent replay', d => d.id === b1.id);
  const c2 = await registerCustomer('cust2', { phoneNumber: '+38349100002' });
  expect(await book(c2.token, s10.startsAt, {}, { 'Idempotency-Key': `k1-${run}` }), 409, 'idempotency key of someone else', d => d.code === 'idempotency_key_reused');
  expect(await book(c2.token, s10.startsAt), 409, 'double booking', d => d.code === 'slot_unavailable' && d.message === 'This time slot is no longer available. Please choose another time.');
  // Buffer: 10 min after 10:00-11:00 means 11:00 is blocked (11:00 < 11:10)
  expect(await book(c2.token, slotAt(day2, '11:00').startsAt), 409, 'buffer respected');
  expect(await book(c2.token, new Date(new Date(s10.startsAt).getTime() + 15 * 60000).toISOString()), 409, 'off-grid start rejected', d => d.code === 'invalid_slot');
  const b2 = expect(await book(c1.token, s12.startsAt), 201, 'second booking same day');
  expect(await book(c1.token, s14.startsAt), 409, 'daily limit', d => d.code === 'daily_limit_reached');
  const day3 = (await api('GET', `/api/facilities/${F}/availability?sportId=${FUTSAL}&date=${D3}`)).data;
  expect(await book(c1.token, slotAt(day3, '10:00').startsAt), 409, 'active limit', d => d.code === 'active_limit_reached');
  expect(await api('GET', '/api/reservations', { token: c1.token }), 200, 'my upcoming', d => d.totalCount === 2);
  expect(await api('GET', `/api/reservations/${b1.id}`, { token: c1.token }), 200, 'my reservation');
  expect(await api('GET', `/api/reservations/${b1.id}`, { token: c2.token }), 404, 'other customer gets 404');
  r = await api('PUT', `/api/reservations/${b2.id}/reschedule`, { token: c1.token, body: { startsAt: slotAt(day3, '19:00').startsAt } });
  expect(r, 200, 'reschedule', d => d.localStartsAt.slice(0, 10) === D3 && d.price === 40);
  expect(await api('PUT', `/api/reservations/${b2.id}/reschedule`, { token: c1.token, body: { startsAt: slotAt(day3, '12:00').startsAt, durationMinutes: 90 } }), 400, 'reschedule cannot change duration');
  expect(await api('POST', `/api/reservations/${b2.id}/cancel`, { token: c1.token, body: { reason: 'Plans changed' } }), 200, 'customer cancel', d => d.status === 'CancelledByCustomer');
  expect(await api('POST', `/api/reservations/${b2.id}/cancel`, { token: c1.token, body: {} }), 409, 'cancel twice');
  expect(await api('GET', '/api/reservations?scope=cancelled', { token: c1.token }), 200, 'cancelled scope', d => d.totalCount === 1);
  expect(await api('GET', '/api/reservations?scope=all', { token: c1.token }), 200, 'all scope', d => d.totalCount === 2);
  // Cancellation deadline (33): raise the facility cutoff to 72h -> a booking 2 days away can't be cancelled
  await api('PUT', `/api/business/facilities/${F}/rules`, { token: owner, body: { durationOptions: [60, 90, 120], bufferMinutes: 10, slotStepMinutes: 30, maxAdvanceDays: 30, cancellationCutoffHours: 72 } });
  expect(await api('POST', `/api/reservations/${b1.id}/cancel`, { token: c1.token, body: {} }), 409, 'cancellation deadline', d => d.code === 'cancellation_deadline_passed');
  expect(await api('PUT', `/api/reservations/${b1.id}/reschedule`, { token: c1.token, body: { startsAt: slotAt(day3, '15:00').startsAt } }), 409, 'change after deadline');
  await api('PUT', `/api/business/facilities/${F}/rules`, { token: owner, body: { durationOptions: [60, 90, 120], bufferMinutes: 10, slotStepMinutes: 30, maxAdvanceDays: 30, cancellationCutoffHours: 24 } });
  expect(await api('GET', `/api/facilities/${F}/availability?sportId=${FUTSAL}&date=${localDate(40)}`), 200, 'beyond booking window', d => d.slots.every(s => !s.available && s.unavailableReason === 'too_far_ahead'));

  // Concurrency: 6 people try the same slot at once -> exactly one wins
  const race = slotAt(day2, '16:00');
  const racers = await Promise.all([...Array(6).keys()].map(i => book(null, race.startsAt, { guest: { name: `Racer ${i}`, phone: `+3834920000${i}` } }, { 'X-Device-Id': `race-${run}-${i}` })));
  check('race: exactly one booking succeeds', racers.filter(x => x.status === 201).length === 1 && racers.filter(x => x.status === 409).length === 5, racers.map(x => x.status));

  // ================================================================= Guests (29, 37)
  section = 'guests';
  const s17 = slotAt(day2, '17:30');
  expect(await book(null, s17.startsAt), 400, 'guest details required', d => d.code === 'guest_details_required');
  r = await book(null, s17.startsAt, { guest: { name: 'Guest One', phone: '+383 49 300 001', email: 'e2e_guest@example.com' } }, { 'X-Device-Id': `dev-${run}` });
  const g1 = expect(r, 201, 'guest booking', d => d.isGuest && d.guestAccessToken && d.guestAccessUrl);
  expect(await api('GET', `/api/reservations/guest/${g1.guestAccessToken}`), 200, 'guest retrieval', d => d.id === g1.id);
  expect(await api('GET', '/api/reservations/guest/not-a-real-token'), 404, 'bad guest token');
  r = await book(null, slotAt(day2, '19:00').startsAt, { guest: { name: 'Guest One', phone: '+38349300001' } }, { 'X-Device-Id': `dev-${run}` });
  expect(r, 201, 'guest second booking same day');
  expect(await book(null, slotAt(day2, '21:00').startsAt, { guest: { name: 'Guest One', phone: '+38349300001' } }, { 'X-Device-Id': `other-${run}` }), 409, 'guest daily limit by phone', d => d.code === 'daily_limit_reached');
  expect(await api('POST', `/api/reservations/guest/${g1.guestAccessToken}/cancel`, { body: { reason: 'Cannot come' } }), 200, 'guest cancel', d => d.status === 'CancelledByCustomer');
  expect(await api('POST', '/api/reservations', { token: 'garbage.token.value', body: { facilityId: F, sportId: FUTSAL, startsAt: s17.startsAt, durationMinutes: 60 } }), 401, 'invalid token never becomes guest');

  // ================================================================= Business reservations (20, 35, 36, 40)
  section = 'business-reservations';
  expect(await api('GET', '/api/business/reservations', { token: owner }), 200, 'list upcoming', d => d.totalCount >= 3);
  expect(await api('GET', `/api/business/reservations?source=SportX&status=Confirmed&facilityId=${F}&sportId=${FUTSAL}`, { token: owner }), 200, 'filters');
  expect(await api('GET', `/api/business/reservations?search=${encodeURIComponent(b1.bookingNumber)}&scope=all`, { token: owner }), 200, 'search by reference', d => d.items.length === 1);
  expect(await api('GET', `/api/business/reservations?from=${D2}&to=${D3}`, { token: owner }), 200, 'date range');
  expect(await api('GET', `/api/business/reservations/${b1.id}`, { token: owner }), 200, 'detail with history', d => d.history.length >= 1);
  expect(await api('GET', `/api/business/reservations/${b1.id}`, { token: owner2 }), 404, 'other business cannot see it');
  r = await api('POST', '/api/business/reservations/external', { token: owner, body: { facilityId: F, sportId: FUTSAL, date: D4, startTime: '20:00', endTime: '21:30', channel: 'WhatsApp', customerName: 'Arben', customerPhone: '+383 49 400 001', internalNotes: 'Pays cash' } });
  const ext = expect(r, 201, 'external reservation', d => d.source === 'External' && d.externalChannel === 'WhatsApp' && d.price === 35);
  expect(await api('POST', '/api/business/reservations/external', { token: owner, body: { facilityId: F, sportId: FUTSAL, date: D4, startTime: '21:00', endTime: '22:00', customerName: 'Clash' } }), 409, 'external conflict', d => d.code === 'slot_unavailable' && d.details.conflicts.length === 1);
  expect(await api('PUT', `/api/business/reservations/${ext.id}/external`, { token: owner, body: { facilityId: F, sportId: FUTSAL, date: D4, startTime: '19:00', endTime: '20:00', channel: 'Phone', customerName: 'Arben K', price: 30 } }), 200, 'edit external', d => d.price === 30 && d.history.some(h => h.eventType === 'Rescheduled'));
  expect(await api('PUT', `/api/business/reservations/${ext.id}/notes`, { token: owner, body: { internalNotes: 'VIP' } }), 200, 'notes', d => d.internalNotes === 'VIP');
  const day4 = (await api('GET', `/api/facilities/${F}/availability?sportId=${FUTSAL}&date=${D4}`)).data;
  check('external booking blocks public slot', slotAt(day4, '19:00')?.available === false, slotAt(day4, '19:00'));
  expect(await api('PUT', `/api/business/reservations/${b1.id}/reschedule`, { token: owner, body: { startsAt: slotAt(day4, '10:00').startsAt, durationMinutes: 90, reason: 'Pitch maintenance' } }), 200, 'business reschedule', d => d.durationMinutes === 90);
  expect(await api('POST', `/api/business/reservations/${b1.id}/complete`, { token: owner }), 409, 'complete before start', d => d.code === 'reservation_not_started');
  expect(await api('POST', `/api/business/reservations/${ext.id}/cancel`, { token: owner, body: {} }), 400, 'business cancel needs reason');
  expect(await api('POST', `/api/business/reservations/${ext.id}/cancel`, { token: owner, body: { reason: 'Customer called' } }), 200, 'business cancel', d => d.status === 'CancelledByBusiness');
  // Past external reservation -> no-show -> corrected to completed
  r = await api('POST', '/api/business/reservations/external', { token: owner, body: { facilityId: F, sportId: FUTSAL, date: localDate(-1), startTime: '10:00', endTime: '11:00', customerName: 'Walk-in', customerPhone: '+38349500001', channel: 'WalkIn' } });
  const past = expect(r, 201, 'past walk-in');
  expect(await api('POST', `/api/business/reservations/${past.id}/no-show`, { token: owner }), 200, 'no-show', d => d.status === 'NoShow');
  expect(await api('POST', `/api/business/reservations/${past.id}/complete`, { token: owner }), 200, 'correct to completed', d => d.status === 'Completed');
  expect(await api('POST', `/api/business/reservations/${past.id}/cancel`, { token: owner, body: { reason: 'x' } }), 409, 'cannot cancel finished');
  expect(await api('GET', '/api/business/reservations?scope=past', { token: owner }), 200, 'past scope', d => d.items.some(x => x.id === past.id));

  // ================================================================= Exceptions & blocks with conflicts (15, 19)
  section = 'availability-admin';
  r = await api('POST', `/api/business/facilities/${F}/exceptions`, { token: owner, body: { startDate: D5, isClosed: true, reason: 'Holiday' } });
  const ex1 = expect(r, 201, 'closure without conflicts');
  expect(await api('GET', `/api/facilities/${F}/availability?sportId=${FUTSAL}&date=${D5}`), 200, 'closed day', d => d.isClosed && d.slots.length === 0);
  expect(await api('GET', `/api/business/facilities/${F}/exceptions`, { token: owner }), 200, 'list exceptions', d => d.length === 1);
  expect(await api('DELETE', `/api/business/facilities/${F}/exceptions/${ex1.id}`, { token: owner }), 204, 'delete exception');
  r = await api('POST', `/api/business/facilities/${F}/exceptions`, { token: owner, body: { startDate: D6, endDate: D6, opensAt: '12:00', closesAt: '16:00' } });
  expect(r, 201, 'special hours');
  expect(await api('GET', `/api/facilities/${F}/availability?sportId=${FUTSAL}&date=${D6}`), 200, 'special hours apply', d => d.slots[0].localStartsAt.endsWith('12:00:00') && d.slots.at(-1).localStartsAt.endsWith('15:00:00'));
  expect(await api('POST', `/api/business/facilities/${F}/exceptions`, { token: owner, body: { startDate: localDate(-2), isClosed: true } }), 400, 'exception in the past');
  // A closure on a day with a reservation needs an explicit decision
  r = await api('POST', `/api/business/facilities/${F}/exceptions`, { token: owner, body: { startDate: D2, isClosed: true, reason: 'Water leak' } });
  expect(r, 409, 'closure conflicts', d => d.code === 'reservation_conflicts' && d.details.conflicts.length >= 1);
  expect(await api('POST', `/api/business/facilities/${F}/exceptions`, { token: owner, body: { startDate: D2, isClosed: true, reason: 'Water leak', conflictResolution: 'KeepReservations' } }), 201, 'closure keeping reservations');
  expect(await api('GET', `/api/business/reservations?from=${D2}&to=${D2}&status=Confirmed`, { token: owner }), 200, 'reservations kept', d => d.totalCount >= 1);
  // Block with conflict -> cancel them
  const dayD8 = (await api('GET', `/api/facilities/${F}/availability?sportId=${FUTSAL}&date=${D8}`)).data;
  const c3 = await registerCustomer('cust3', { phoneNumber: '+38349100003' });
  const victim = expect(await book(c3.token, slotAt(dayD8, '15:00').startsAt), 201, 'booking to be blocked');
  r = await api('POST', `/api/business/facilities/${F}/blocks`, { token: owner, body: { date: D8, startTime: '14:00', endTime: '17:00', reason: 'Maintenance', internalNote: 'Lights' } });
  expect(r, 409, 'block conflicts', d => d.code === 'reservation_conflicts');
  r = await api('POST', `/api/business/facilities/${F}/blocks`, { token: owner, body: { date: D8, startTime: '14:00', endTime: '17:00', reason: 'Maintenance', conflictResolution: 'CancelReservations' } });
  const block = expect(r, 201, 'block cancelling reservations');
  expect(await api('GET', `/api/reservations/${victim.id}`, { token: c3.token }), 200, 'customer sees cancellation', d => d.status === 'CancelledByBusiness');
  expect(await api('POST', `/api/business/facilities/${F}/blocks`, { token: owner, body: { date: D8, startTime: '16:00', endTime: '18:00', reason: 'Event' } }), 409, 'overlapping block', d => d.code === 'block_overlap');
  expect(await api('GET', `/api/facilities/${F}/availability?sportId=${FUTSAL}&date=${D8}`), 200, 'block hides slots', d => !slotAt(d, '15:00').available);
  expect(await api('GET', `/api/business/facilities/${F}/blocks`, { token: owner }), 200, 'list blocks', d => d.length === 1);
  expect(await api('GET', `/api/business/blocks?facilityId=${F}`, { token: owner }), 200, 'business blocks');
  expect(await api('DELETE', `/api/business/facilities/${F}/blocks/${block.id}`, { token: owner }), 204, 'delete block');
  expect(await api('DELETE', `/api/business/facilities/${F}`, { token: owner }), 409, 'facility with bookings cannot be deleted', d => d.code === 'facility_has_bookings');

  // ================================================================= Dashboard & analytics (39, 41)
  section = 'dashboard';
  expect(await api('GET', '/api/business/dashboard', { token: owner }), 200, 'dashboard', d => d.upcomingReservations.length >= 1 && d.canTakeReservations && d.subscription.hasActiveSubscription);
  expect(await api('GET', `/api/business/analytics?from=${localDate(-3)}&to=${D9}`, { token: owner }), 200, 'analytics', d => d.totalReservations >= 3 && d.externalReservations >= 1 && d.byFacility.length >= 1 && d.byDay.length === 13);
  expect(await api('GET', `/api/business/analytics?facilityId=${F}`, { token: owner }), 200, 'analytics per facility');

  // ================================================================= Notifications outbox (38)
  section = 'notifications';
  expect(await api('POST', '/api/admin/jobs/notifications/run', { token: admin }), 200, 'send outbox');
  const sent = Number(sql(`SELECT count(*) FROM "NotificationMessages" WHERE "BusinessId" = '${businessId}' AND "Status" = 2 AND "Template" LIKE 'business.reservation_%'`));
  check('business reservation emails sent', sent >= 3, `sent=${sent}`);
  const customerMails = Number(sql(`SELECT count(*) FROM "NotificationMessages" WHERE "BusinessId" = '${businessId}' AND "Template" LIKE 'customer.%'`));
  check('no customer emails while flag off', customerMails === 0, `customer=${customerMails}`);

  // ================================================================= Staff (57)
  section = 'staff';
  const staff = await registerCustomer('staff');
  expect(await api('POST', '/api/business/staff/invitations', { token: owner, body: { email: staff.email, permissions: 'ManageReservations' } }), 404, 'staff invitations disabled', d => d.code === 'feature_disabled');
  expect(await api('POST', '/api/admin/feature-flags/staff_invitations/enable', { token: admin }), 200, 'enable staff flag');
  r = await api('POST', '/api/business/staff/invitations', { token: owner, body: { email: staff.email, permissions: 'ManageReservations, ViewAnalytics' } });
  const inv = expect(r, 200, 'invite staff', d => d.status === 'Pending');
  expect(await api('POST', `/api/business/staff/invitations/${inv.id}/resend`, { token: owner }), 200, 'resend invitation');
  expect(await api('GET', '/api/business/staff/invitations', { token: owner }), 200, 'list invitations');
  const body = sql(`SELECT "Body" FROM "NotificationMessages" WHERE "Recipient" = '${staff.email}' AND "Template" = 'business.staff_invitation' ORDER BY "CreatedAt" DESC LIMIT 1`);
  const token = decodeURIComponent(/token=([^\s]+)/.exec(body)?.[1] ?? '');
  expect(await api('POST', '/api/invitations/accept', { token: c2.token, body: { token } }), 403, 'invitation for another email', d => d.code === 'invitation_email_mismatch');
  expect(await api('POST', '/api/invitations/accept', { token: staff.token, body: { token } }), 200, 'accept invitation');
  expect(await api('GET', '/api/business/reservations', { token: staff.token }), 200, 'staff sees reservations');
  expect(await api('POST', '/api/business/facilities', { token: staff.token, body: { name: 'Nope' } }), 403, 'staff without facility permission', d => d.code === 'missing_permission');
  expect(await api('GET', '/api/business/staff', { token: owner }), 200, 'members', d => d.length === 2);
  const staffId = (await api('GET', '/api/auth/me', { token: staff.token })).data.id;
  expect(await api('PUT', `/api/business/staff/${staffId}`, { token: owner, body: { permissions: 'ManageReservations', isActive: false } }), 200, 'deactivate staff');
  expect(await api('GET', '/api/business/reservations', { token: staff.token }), 403, 'deactivated staff blocked', d => d.code === 'no_business');
  r = await api('POST', '/api/business/staff/invitations', { token: owner, body: { email: `e2e_x_${run}@example.com` } });
  expect(await api('DELETE', `/api/business/staff/invitations/${r.data.id}`, { token: owner }), 204, 'revoke invitation');

  // ================================================================= Multiple businesses (58) with owner2
  section = 'multi-business';
  const second = { businessName: `E2E Second ${run}`, addressLine: 'Rr. 2', city: 'Prizren', country: 'XK', phone: '+38344999000', email: `e2e_second_${run}@example.com`, preferredLanguage: 'sq' };
  expect(await api('POST', '/api/business', { token: owner2, body: second }), 404, 'second business disabled', d => d.code === 'feature_disabled');
  await api('POST', '/api/admin/feature-flags/multiple_businesses/enable', { token: admin });
  r = await api('POST', '/api/business', { token: owner2, body: second });
  const biz2 = expect(r, 201, 'create second business');
  expect(await api('GET', '/api/business/mine', { token: owner2 }), 200, 'two businesses', d => d.length === 2);
  expect(await api('GET', '/api/business/profile', { token: owner2 }), 400, 'selection required', d => d.code === 'business_selection_required');
  expect(await api('GET', '/api/business/profile', { token: owner2, business: biz2.id }), 200, 'with X-Business-Id', d => d.id === biz2.id);
  expect(await api('GET', '/api/business/profile', { token: owner2, business: businessId }), 404, 'header for foreign business');

  // ================================================================= Venues (59)
  section = 'venues';
  await api('POST', '/api/admin/feature-flags/venues/enable', { token: admin });
  r = await api('POST', '/api/business/venues', { token: owner, body: { name: 'E2E Sports Center', city: 'Prishtinë', facilityIds: [F] } });
  const venue = expect(r, 201, 'create venue', d => d.facilityIds.includes(F));
  expect(await api('GET', '/api/business/venues', { token: owner }), 200, 'list venues');
  expect(await api('GET', `/api/venues/${venue.slug}`), 200, 'public venue', d => d.facilities.length === 1 && d.sports.length === 2);
  expect(await api('PUT', `/api/business/venues/${venue.id}`, { token: owner, body: { name: 'E2E Sports Center', facilityIds: [] } }), 200, 'update venue');
  expect(await api('DELETE', `/api/business/venues/${venue.id}`, { token: owner }), 204, 'delete venue');

  // ================================================================= SMS verification (61)
  section = 'sms';
  await api('POST', '/api/admin/feature-flags/sms_verification/enable', { token: admin });
  // Phone with a recent no-show must verify
  const nsPhone = '+38349600001';
  r = await api('POST', '/api/business/reservations/external', { token: owner, body: { facilityId: F, sportId: FUTSAL, date: localDate(-2), startTime: '12:00', endTime: '13:00', customerName: 'Flaky', customerPhone: nsPhone } });
  await api('POST', `/api/business/reservations/${r.data.id}/no-show`, { token: owner });
  const smsSlot = slotAt(dayD8, '20:00').startsAt;
  expect(await book(null, smsSlot, { guest: { name: 'Flaky', phone: nsPhone } }, { 'X-Device-Id': `sms-${run}` }), 403, 'verification required', d => d.code === 'phone_verification_required');
  expect(await api('POST', '/api/verification/phone/send', { body: { phone: nsPhone } }), 204, 'send code');
  const smsBody = sql(`SELECT "Body" FROM "NotificationMessages" WHERE "Recipient" = '${nsPhone}' ORDER BY "CreatedAt" DESC LIMIT 1`);
  const smsCode = /(\d{6})/.exec(smsBody)?.[1];
  expect(await api('POST', '/api/verification/phone/verify', { body: { phone: nsPhone, code: '000000' === smsCode ? '111111' : '000000' } }), 400, 'wrong code');
  r = await api('POST', '/api/verification/phone/verify', { body: { phone: nsPhone, code: smsCode } });
  const vt = expect(r, 200, 'verify code', d => d.verificationToken);
  expect(await book(null, smsSlot, { guest: { name: 'Flaky', phone: nsPhone }, phoneVerificationToken: vt.verificationToken }, { 'X-Device-Id': `sms-${run}` }), 201, 'verified guest books');
  await api('POST', '/api/admin/feature-flags/sms_verification/disable', { token: admin });

  // ================================================================= Online payments (62)
  section = 'payments';
  await api('POST', '/api/admin/feature-flags/customer_payments/enable', { token: admin });
  await api('PUT', `/api/business/facilities/${F}/rules`, { token: owner, body: { durationOptions: [60, 90, 120], bufferMinutes: 10, slotStepMinutes: 30, maxAdvanceDays: 30, cancellationCutoffHours: 24, requiresOnlinePayment: true } });
  const c4 = await registerCustomer('cust4');
  const dayD9 = (await api('GET', `/api/facilities/${F}/availability?sportId=${FUTSAL}&date=${D9}`)).data;
  r = await book(c4.token, slotAt(dayD9, '10:00').startsAt);
  const pb = expect(r, 201, 'paid booking pending', d => d.status === 'Pending' && d.paymentStatus === 'Pending' && d.checkoutUrl && d.expiresAt);
  check('pending holds the slot', !(await api('GET', `/api/facilities/${F}/availability?sportId=${FUTSAL}&date=${D9}`)).data.slots.find(s => s.startsAt === slotAt(dayD9, '10:00').startsAt).available, 'slot free');
  const payId = sql(`SELECT "Id" FROM "Payments" WHERE "BookingId" = '${pb.id}'`);
  expect(await api('POST', `/api/payments/mock/${payId}/complete?succeeded=true`), 204, 'pay reservation');
  expect(await api('GET', `/api/reservations/${pb.id}`, { token: c4.token }), 200, 'confirmed after payment', d => d.status === 'Confirmed' && d.paymentStatus === 'Paid');
  expect(await api('POST', `/api/reservations/${pb.id}/cancel`, { token: c4.token, body: {} }), 200, 'cancel paid booking refunds', d => d.paymentStatus === 'Refunded');
  r = await book(c4.token, slotAt(dayD9, '12:00').startsAt);
  sql(`UPDATE "Bookings" SET "ExpiresAt" = now() - interval '1 minute' WHERE "Id" = '${r.data.id}'`);
  expect(await api('POST', '/api/admin/jobs/reservation-expiry/run', { token: admin }), 200, 'expiry job');
  expect(await api('GET', `/api/reservations/${r.data.id}`, { token: c4.token }), 200, 'unpaid booking expired', d => d.status === 'Expired');
  await api('PUT', `/api/business/facilities/${F}/rules`, { token: owner, body: { durationOptions: [60, 90, 120], bufferMinutes: 10, slotStepMinutes: 30, maxAdvanceDays: 30, cancellationCutoffHours: 24, requiresOnlinePayment: false } });
  await api('POST', '/api/admin/feature-flags/customer_payments/disable', { token: admin });

  // ================================================================= Reviews (63)
  section = 'reviews';
  // Move c1's booking into the past so it can be completed and reviewed.
  sql(`UPDATE "Bookings" SET "StartsAt" = now() - interval '3 hours', "EndsAt" = now() - interval '2 hours', "BlockedUntil" = now() - interval '2 hours' WHERE "Id" = '${b1.id}'`);
  expect(await api('POST', `/api/business/reservations/${b1.id}/complete`, { token: owner }), 200, 'complete');
  expect(await api('POST', '/api/reviews', { token: c1.token, body: { bookingId: b1.id, rating: 5 } }), 404, 'reviews disabled', d => d.code === 'feature_disabled');
  await api('POST', '/api/admin/feature-flags/reviews/enable', { token: admin });
  expect(await api('GET', `/api/reservations/${b1.id}`, { token: c1.token }), 200, 'canReview', d => d.canReview);
  r = await api('POST', '/api/reviews', { token: c1.token, body: { bookingId: b1.id, rating: 5, comment: 'Great pitch!' } });
  const review = expect(r, 201, 'write review');
  expect(await api('POST', '/api/reviews', { token: c1.token, body: { bookingId: b1.id, rating: 4 } }), 409, 'one review per booking');
  expect(await api('POST', '/api/reviews', { token: c2.token, body: { bookingId: b1.id, rating: 1 } }), 404, 'cannot review others booking');
  expect(await api('GET', `/api/facilities/${slug}/reviews`), 200, 'public reviews', d => d.totalCount === 1 && d.items[0].authorName === 'Cust O.');
  expect(await api('GET', `/api/facilities/${slug}`), 200, 'rating on details', d => d.averageRating === 5 && d.reviewCount === 1);
  expect(await api('PUT', `/api/business/reviews/${review.id}/reply`, { token: owner, body: { reason: 'Thank you!' } }), 200, 'business reply', d => d.businessReply === 'Thank you!');
  expect(await api('POST', `/api/reports/Review/${review.id}`, { token: c2.token, body: { reason: 'Looks fake' } }), 204, 'report review');
  r = await api('GET', '/api/admin/reports?status=Open', { token: admin });
  const report = expect(r, 200, 'open reports', d => d.items.some(x => x.targetId === review.id)).items.find(x => x.targetId === review.id);
  expect(await api('PUT', `/api/admin/reviews/${review.id}/status`, { token: admin, body: { status: 'Hidden', note: 'Under review' } }), 200, 'hide review');
  expect(await api('GET', `/api/facilities/${slug}/reviews`), 200, 'hidden review not public', d => d.totalCount === 0);
  expect(await api('POST', `/api/admin/reports/${report.id}/resolve`, { token: admin, body: { status: 'Resolved', note: 'Hidden' } }), 200, 'resolve report');

  // ================================================================= Players & teams (64, 65)
  section = 'community';
  await api('POST', '/api/admin/feature-flags/player_profiles/enable', { token: admin });
  await api('POST', '/api/admin/feature-flags/teams/enable', { token: admin });
  expect(await api('PUT', '/api/players/me', { token: c1.token, body: { visibility: 'Public', bio: 'Futsal winger', city: 'Prishtinë', sports: [{ sportId: FUTSAL, skillLevel: 'Intermediate', preferredPosition: 'Winger' }] } }), 200, 'save player profile');
  expect(await api('PUT', '/api/players/me', { token: c2.token, body: { visibility: 'Private', sports: [{ sportId: FUTSAL }] } }), 200, 'private profile');
  expect(await api('GET', `/api/players?sportId=${FUTSAL}&city=Prishtinë`, { token: c3.token }), 200, 'find teammates', d => d.items.some(p => p.username === c1.username) && !d.items.some(p => p.username === c2.username));
  expect(await api('GET', `/api/players/${c1.username}`), 200, 'public profile anonymous', d => !('email' in d));
  expect(await api('GET', `/api/players/${c2.username}`, { token: c3.token }), 404, 'private profile hidden');
  expect(await api('GET', '/api/players/me', { token: c2.token }), 200, 'own private profile');
  r = await api('POST', '/api/teams', { token: c1.token, body: { name: `E2E FC ${run}`, sportId: FUTSAL, city: 'Prishtinë', maxMembers: 3 } });
  const team = expect(r, 201, 'create team', d => d.memberCount === 1);
  expect(await api('POST', `/api/teams/${team.id}/invitations`, { token: c1.token, body: { username: c2.username } }), 200, 'invite');
  expect(await api('POST', `/api/teams/${team.id}/invitations`, { token: c2.token, body: { username: c3.username } }), 403, 'only captain invites');
  expect(await api('POST', `/api/teams/${team.id}/invitations/respond?accept=true`, { token: c2.token }), 200, 'accept invite', d => d.memberCount === 2);
  expect(await api('POST', `/api/teams/${team.id}/requests`, { token: c3.token }), 200, 'request to join', d => d.myStatus === 'Requested');
  expect(await api('POST', `/api/teams/${team.id}/requests/${c3.id ?? (await api('GET', '/api/auth/me', { token: c3.token })).data.id}?approve=true`, { token: c1.token }), 200, 'approve request', d => d.memberCount === 3);
  expect(await api('POST', `/api/teams/${team.id}/requests`, { token: c4.token }), 409, 'team full', d => d.code === 'team_full');
  expect(await api('GET', `/api/teams?sportId=${FUTSAL}`), 200, 'search teams', d => d.items.some(t => t.id === team.id));
  expect(await api('GET', '/api/teams/mine', { token: c2.token }), 200, 'my teams', d => d.length === 1);
  const c3Id = (await api('GET', '/api/auth/me', { token: c3.token })).data.id;
  expect(await api('DELETE', `/api/teams/${team.id}/members/${c3Id}`, { token: c1.token }), 200, 'remove member', d => d.memberCount === 2);
  expect(await api('POST', `/api/reports/Team/${team.id}`, { token: c3.token, body: { reason: 'Rude captain' } }), 204, 'report team');
  expect(await api('POST', `/api/teams/${team.id}/leave`, { token: c1.token }), 204, 'captain leaves (handover)');
  expect(await api('GET', `/api/teams/${team.id}`, { token: c2.token }), 200, 'new captain', d => d.captainUserId === (c2.id));
  expect(await api('PUT', `/api/teams/${team.id}`, { token: c2.token, body: { name: `E2E FC ${run}`, sportId: FUTSAL, maxMembers: 10 } }), 200, 'update team');
  expect(await api('DELETE', `/api/teams/${team.id}`, { token: c2.token }), 204, 'delete team');

  // ================================================================= Recommendations & promotions (66)
  section = 'promotions';
  await api('POST', '/api/admin/feature-flags/recommendations/enable', { token: admin });
  await api('POST', '/api/admin/feature-flags/promoted_listings/enable', { token: admin });
  expect(await api('GET', '/api/recommendations', { token: c1.token }), 200, 'recommendations with reasons', d => d.some(x => x.facility.id === F && x.reasons.length > 0));
  r = await api('POST', '/api/admin/promotions', { token: admin, body: { facilityId: F, startsAt: new Date(Date.now() - 60000).toISOString(), endsAt: new Date(Date.now() + 86400000).toISOString() } });
  const promo = expect(r, 201, 'create promotion', d => d.label === 'Sponsored');
  expect(await api('GET', '/api/facilities?sport=futsal'), 200, 'sponsored shown separately', d => d.sponsored.some(c => c.id === F && c.isSponsored && c.sponsoredLabel === 'Sponsored'));
  expect(await api('GET', '/api/admin/promotions', { token: admin }), 200, 'list promotions');
  expect(await api('PUT', `/api/admin/promotions/${promo.id}`, { token: admin, body: { facilityId: F, startsAt: new Date().toISOString(), endsAt: new Date(Date.now() + 3600000).toISOString(), isActive: false } }), 200, 'update promotion');
  expect(await api('DELETE', `/api/admin/promotions/${promo.id}`, { token: admin }), 204, 'delete promotion');

  // ================================================================= Admin management (47-50)
  section = 'admin';
  expect(await api('GET', '/api/admin/dashboard', { token: admin }), 200, 'admin dashboard', d => d.publishedFacilities >= 1 && d.recentActivity.length > 0);
  expect(await api('GET', '/api/admin/dashboard', { token: c1.token }), 403, 'customer blocked from admin');
  expect(await api('GET', `/api/admin/analytics?from=${localDate(-3)}&to=${D9}`, { token: admin }), 200, 'platform analytics', d => d.byDay.length === 13 && d.subscriptionRevenue >= 19.99);
  expect(await api('GET', `/api/admin/audit-logs?action=reservation.&businessId=${businessId}`, { token: admin }), 200, 'audit logs', d => d.totalCount > 0);
  expect(await api('GET', `/api/admin/users?role=User&search=e2e_cust`, { token: admin }), 200, 'admin users', d => d.totalCount >= 3);
  expect(await api('GET', `/api/admin/users/${c1.id}`, { token: admin }), 200, 'admin user detail', d => d.reservations >= 1);
  expect(await api('POST', `/api/admin/users/${c3Id}/suspend`, { token: admin, body: { reason: 'Abuse' } }), 200, 'suspend user', d => d.status === 'Suspended');
  expect(await api('GET', '/api/auth/me', { token: c3.token }), 401, 'suspended user locked out');
  expect(await api('POST', '/api/auth/login', { body: { email: c3.email, password: 'Password1!' } }), 403, 'suspended login', d => d.code === 'account_suspended');
  expect(await api('POST', `/api/admin/users/${c3Id}/activate`, { token: admin, body: {} }), 200, 'reactivate user');
  expect(await api('PUT', `/api/admin/users/${c3Id}/role`, { token: admin, body: { role: 'Admin' } }), 200, 'change role');
  expect(await api('PUT', `/api/admin/users/${c3Id}/role`, { token: admin, body: { role: 'User' } }), 200, 'change role back');
  expect(await api('GET', '/api/admin/businesses?search=E2E', { token: admin }), 200, 'admin businesses', d => d.totalCount >= 2);
  expect(await api('GET', `/api/admin/businesses/${businessId}`, { token: admin }), 200, 'admin business detail', d => d.registrationNumber && d.members.length >= 1);
  expect(await api('POST', `/api/admin/businesses/${businessId}/suspend`, { token: admin, body: { reason: 'Check documents' } }), 200, 'suspend business');
  expect(await api('GET', `/api/facilities/${F}`), 404, 'suspended business hidden');
  expect(await api('POST', '/api/reservations', { token: c2.token, body: { facilityId: F, sportId: FUTSAL, startsAt: slotAt(dayD9, '14:00').startsAt, durationMinutes: 60 } }), 409, 'no bookings while suspended', d => d.code === 'facility_unavailable');
  expect(await api('POST', `/api/admin/businesses/${businessId}/unsuspend`, { token: admin }), 200, 'unsuspend business', d => d.status === 'Active');
  expect(await api('GET', `/api/admin/facilities?businessId=${businessId}`, { token: admin }), 200, 'admin facilities');
  expect(await api('GET', `/api/admin/facilities/${F}`, { token: admin }), 200, 'admin facility');
  expect(await api('POST', `/api/admin/facilities/${F}/suspend`, { token: admin, body: { reason: 'Photos' } }), 200, 'suspend facility');
  expect(await api('POST', `/api/business/facilities/${F}/publish`, { token: owner }), 403, 'owner cannot publish suspended', d => d.code === 'facility_suspended');
  expect(await api('POST', `/api/admin/facilities/${F}/unsuspend`, { token: admin }), 200, 'unsuspend facility', d => d.status === 'Hidden');
  expect(await api('POST', `/api/business/facilities/${F}/publish`, { token: owner }), 200, 'publish again');
  expect(await api('POST', `/api/admin/facilities/${F}/hide`, { token: admin, body: { reason: 'test' } }), 200, 'admin hide');
  await api('POST', `/api/business/facilities/${F}/publish`, { token: owner });
  expect(await api('GET', `/api/admin/reservations?businessId=${businessId}&scope=all`, { token: admin }), 200, 'admin reservations', d => d.totalCount > 3);
  const adminCancel = (await api('GET', `/api/business/reservations?status=Confirmed&scope=upcoming`, { token: owner })).data.items[0];
  expect(await api('GET', `/api/admin/reservations/${adminCancel.id}`, { token: admin }), 200, 'admin reservation detail');
  expect(await api('POST', `/api/admin/reservations/${adminCancel.id}/cancel`, { token: admin, body: { reason: 'Dispute' } }), 200, 'admin cancel', d => d.status === 'CancelledByBusiness' && d.cancellationReason.includes('Platform admin'));
  // Catalogue
  r = await api('POST', '/api/admin/catalog/sports', { token: admin, body: { name: `E2E Sport ${run}`, category: 'TeamSport', defaultMinParticipants: 2, defaultMaxParticipants: 10, nameTranslations: { sq: 'Sport E2E', fr: 'ignored' } } });
  const sport = expect(r, 201, 'create sport', d => d.nameTranslations.sq === 'Sport E2E' && !d.nameTranslations.fr);
  expect(await api('GET', '/api/sports', { headers: { 'Accept-Language': 'sq' } }), 200, 'new sport translated', d => d.some(s => s.name === 'Sport E2E'));
  expect(await api('PUT', `/api/admin/catalog/sports/${sport.id}`, { token: admin, body: { name: sport.name, category: 'TeamSport', isActive: false, defaultMinParticipants: 2, defaultMaxParticipants: 10 } }), 200, 'deactivate sport');
  expect(await api('POST', '/api/business/facilities', { token: owner2, business: biz2.id, body: { name: 'With inactive sport', sportIds: [sport.id] } }), 400, 'inactive sport not selectable');
  expect(await api('GET', '/api/admin/catalog/sports', { token: admin }), 200, 'admin sports');
  r = await api('POST', '/api/admin/catalog/amenities', { token: admin, body: { name: `E2E Sauna ${run}`, nameTranslations: { sq: 'Sauna' } } });
  const amenity = expect(r, 201, 'create amenity');
  expect(await api('PUT', `/api/admin/catalog/amenities/${amenity.id}`, { token: admin, body: { name: `E2E Sauna ${run}`, icon: 'sauna' } }), 200, 'update amenity');
  expect(await api('GET', '/api/admin/catalog/amenities', { token: admin }), 200, 'admin amenities');
  expect(await api('DELETE', `/api/admin/catalog/amenities/${PARKING}`, { token: admin }), 409, 'amenity in use');
  expect(await api('DELETE', `/api/admin/catalog/amenities/${amenity.id}`, { token: admin }), 204, 'delete amenity');
  // Flags & subscriptions lists
  expect(await api('GET', '/api/admin/feature-flags', { token: admin }), 200, 'admin flags');
  expect(await api('POST', '/api/admin/feature-flags', { token: admin, body: { key: `e2e_flag_${run}`, description: 'test' } }), 201, 'create flag');
  expect(await api('POST', '/api/admin/feature-flags/does_not_exist/enable', { token: admin }), 404, 'unknown flag');
  expect(await api('GET', '/api/admin/subscriptions?status=Active', { token: admin }), 200, 'admin subscriptions', d => d.totalCount >= 1);
  expect(await api('GET', '/api/admin/plans', { token: admin }), 200, 'admin plans');

  // ================================================================= Subscription lifecycle jobs (44-46)
  section = 'subscription-jobs';
  const subId = sql(`SELECT "Id" FROM "BusinessSubscriptions" WHERE "BusinessId" = '${businessId}' AND "Status" = 2`);
  sql(`UPDATE "BusinessSubscriptions" SET "CurrentPeriodEnd" = now() - interval '1 minute' WHERE "Id" = '${subId}'`);
  expect(await api('POST', '/api/admin/jobs/subscriptions/run', { token: admin }), 200, 'renewal job');
  expect(await api('GET', '/api/business/subscription', { token: owner }), 200, 'renewed', d => d.status === 'Active' && new Date(d.currentPeriodEnd) > new Date() && d.payments.filter(p => p.status === 'Succeeded').length === 2);
  // Failed renewal -> grace -> expiry
  sql(`UPDATE "BusinessSubscriptions" SET "Status" = 3, "GraceEndsAt" = now() + interval '2 days', "GraceNoticeSent" = false WHERE "Id" = '${subId}'`);
  expect(await api('POST', '/api/admin/jobs/subscriptions/run', { token: admin }), 200, 'grace reminder job');
  check('grace reminder emailed', Number(sql(`SELECT count(*) FROM "NotificationMessages" WHERE "BusinessId" = '${businessId}' AND "Template" = 'subscription.grace_ending'`)) === 1, 'no reminder');
  expect(await api('GET', '/api/business/subscription', { token: owner }), 200, 'past due notice', d => d.status === 'PastDue' && d.notice.includes('Pay before'));
  expect(await api('GET', `/api/facilities/${F}`), 200, 'still public during grace');
  sql(`UPDATE "BusinessSubscriptions" SET "GraceEndsAt" = now() - interval '1 minute' WHERE "Id" = '${subId}'`);
  expect(await api('POST', '/api/admin/jobs/subscriptions/run', { token: admin }), 200, 'expiry job');
  expect(await api('GET', '/api/business/subscription', { token: owner }), 200, 'expired -> inactive', d => d.businessStatus === 'Inactive' && d.dataRetentionUntil && d.notice);
  expect(await api('GET', `/api/facilities/${F}`), 404, 'inactive business hidden');
  expect(await api('GET', '/api/business/reservations', { token: owner }), 200, 'history still visible');
  check('confirmed reservations not cancelled', Number(sql(`SELECT count(*) FROM "Bookings" WHERE "BusinessId" = '${businessId}' AND "Status" = 2`)) >= 1, 'none');
  expect(await api('POST', '/api/business/reservations/external', { token: owner, body: { facilityId: F, sportId: FUTSAL, date: D9, startTime: '08:00', endTime: '09:00' } }), 403, 'no new reservations when inactive', d => d.code === 'subscription_expired');
  r = await api('POST', '/api/business/subscription/pay', { token: owner });
  expect(r, 200, 'pay outstanding');
  await api('POST', `/api/payments/mock/${r.data.paymentId}/complete?succeeded=true`);
  expect(await api('GET', '/api/business/subscription', { token: owner }), 200, 'reactivated', d => d.status === 'Active' && d.businessStatus === 'Active');
  expect(await api('GET', `/api/facilities/${F}`), 200, 'public again');
  // Retention: owner2's second business inactive for > retention period -> anonymized
  sql(`UPDATE "Businesses" SET "Status" = 3, "InactiveSince" = now() - interval '3 years', "RetentionUntil" = now() - interval '1 day' WHERE "Id" = '${biz2.id}'`);
  expect(await api('GET', '/api/admin/retention', { token: admin }), 200, 'retention list', d => d.some(x => x.businessId === biz2.id));
  expect(await api('POST', '/api/admin/jobs/retention/run', { token: admin }), 200, 'retention job');
  check('business anonymized', sql(`SELECT "BusinessName" FROM "Businesses" WHERE "Id" = '${biz2.id}'`).startsWith('Deleted business'), 'not anonymized');

  // ================================================================= Account (03, 28, 52)
  section = 'account';
  expect(await api('GET', '/api/account/export', { token: c1.token }), 200, 'data export', d => d.profile && d.reservations.length >= 1 && d.reviews.length === 1);
  r = await api('PUT', '/api/auth/password', { token: c4.token, body: { currentPassword: 'Password1!', newPassword: 'NewPassword2!' } });
  expect(r, 200, 'change password');
  c4.token = r.data.accessToken;
  expect(await api('DELETE', '/api/auth/me', { token: c4.token, body: { password: 'NewPassword2!' } }), 204, 'delete account');
  expect(await api('POST', '/api/auth/login', { body: { email: c4.email, password: 'NewPassword2!' } }), 403, 'deleted login', d => d.code === 'account_deactivated');
  expect(await api('POST', '/api/auth/reactivate', { body: { email: c4.email, password: 'NewPassword2!' } }), 200, 'reactivate');
  r = await api('POST', '/api/auth/login', { body: { email: c4.email, password: 'NewPassword2!' } });
  expect(await api('POST', '/api/auth/logout', { body: { refreshToken: r.data.refreshToken } }), 204, 'logout');
  expect(await api('POST', '/api/auth/logout-all', { token: r.data.accessToken }), 204, 'logout all');

  // Error contract
  section = 'errors';
  expect(await api('POST', '/api/reservations', { token: c1.token, body: '{bad json' }), 400, 'bad JSON', d => d.code === 'validation_failed' && d.traceId);
  expect(await api('PUT', `/api/business/facilities/${F}/hours`, { token: owner, body: { days: [{ dayOfWeek: 'Monday', windows: [{ opensAt: '25:99', closesAt: '10:00' }] }] } }), 400, 'bad time format');
  expect(await api('GET', '/api/business/facilities', { token: owner, headers: { 'X-Business-Id': 'not-a-guid' } }), 400, 'bad X-Business-Id', d => d.code === 'invalid_business_id');
}

try {
  await main();
} catch (e) {
  check('script crashed', false, e.stack);
}

const failed = results.filter(r => !r.ok);
const bySection = {};
for (const r of results) { bySection[r.section] ??= [0, 0]; bySection[r.section][r.ok ? 0 : 1]++; }
console.log('\n=== SUMMARY ===');
for (const [s, [p, f]] of Object.entries(bySection)) console.log(`${s.padEnd(22)} ${p} passed${f ? `, ${f} FAILED` : ''}`);
console.log(`TOTAL: ${results.length - failed.length}/${results.length} passed`);
