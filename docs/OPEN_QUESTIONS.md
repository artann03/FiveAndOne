# Open questions & decisions made during the backend run

Every point below was decided with a sensible default so work could continue. Each is easy to change.
Please confirm or correct them. **Bold = most important to answer before launch.**

## Setup that changed for the team

- **The dev JWT key and SuperAdmin password are no longer in `appsettings.Development.json`** (GitHub blocked pushing secrets). Each developer runs once:
  ```
  dotnet user-secrets set "Jwt:Key" "<any random string of 64+ characters>" --project FiveAndOne
  dotnet user-secrets set "SuperAdmin:Password" "<password>" --project FiveAndOne
  ```
  Without the key the API refuses to start, with a message explaining this.
- `dotnet ef database update` applies the new migration `SportXCoreModel`. Existing business profiles and facilities are migrated automatically.

## Architecture

| # | Decision | Alternative |
|---|---|---|
| A1 | New services use `ApplicationDbContext` directly (EF Core is already a repository/unit of work). The auth repositories were kept. | Write a repository per entity (more boilerplate, no behaviour change). |
| A2 | `BusinessProfile` became **`Business`**. Facilities, reservations and subscriptions belong to the business, and people get access through **`BusinessMember`** (Owner or Staff with permission flags). This is what stories 04, 57 and 58 need. | Keep everything on the owner user (blocks staff and multiple businesses). |
| A3 | Business endpoints accept an optional **`X-Business-Id`** header. It's only needed when a user belongs to more than one business. | Business id in every route (`/api/businesses/{id}/...`). |
| A4 | Email, SMS, payments, file storage and geocoding are **mocks** behind interfaces (see Providers below). | — |
| A5 | Background jobs are plain `BackgroundService`s: the outbox sender (15 s), payment-hold expiry (1 min), and subscriptions/retention (hourly). | Hangfire/Quartz if jobs grow. |

## Business & facilities

| # | Story | Decision | Alternative |
|---|---|---|---|
| B1 | 06 | **Kosovo registration number accepted as 8 or 9 digits** (old business no. / NUI). Other countries: 4–30 letters/digits. Unique per platform. Never returned by business or public endpoints (admins see it). | **Confirm the exact ARBK format and whether the fiscal number (NF) is also needed.** |
| B2 | 06 | `POST /api/auth/register-business` creates the owner account + business in one transaction and logs in. Username is optional (generated from the email). | Separate signup + profile steps (the old flow still works: `PUT /api/business/profile` creates the business on first save). |
| B3 | 08 | **Facility limit comes from the active plan's `MaxFacilities`.** Without a subscription a business can prepare 1 draft (`Facilities:MaxFacilitiesWithoutSubscription`) but **can't publish** (`subscription_required`). | **Allow publishing without a plan (free tier)?** |
| B4 | 08/12 | **Publishing now also requires weekly hours and at least one price** (otherwise the listing can't be booked). | Publish without them and show "not bookable yet". |
| B5 | 12 | A business that is Suspended (admin) or Inactive (unpaid) disappears from discovery without changing facility statuses, so paying or unsuspending restores everything. | Set every facility to Hidden. |
| B6 | 13 | Price rules: optional sport / duration / weekday / start-time window; PerReservation or PerHour; the most specific match wins, then Priority. Only EUR is accepted (the column supports other currencies). A slot without a matching price can't be booked (`no_price`). | — |
| B7 | 14 | Overnight hours are allowed (closes ≤ opens = past midnight), and slots belong to the day the window starts. An overnight window may not run into the next day's opening. | Disallow overnight. |
| B8 | 15/19 | Closures and blocks that hit existing reservations return **409 `reservation_conflicts`** with the list; the owner resends with `conflictResolution: CancelReservations` or `KeepReservations`. | Always block, or always cancel. |
| B9 | 16 | Durations default `[60]`, buffer 0, slots every 30 min (5/10/15/20/30/60 allowed), booking window 0 min – 30 days. Sports can override durations and buffer. | — |
| B10 | 19 | Overlapping manual blocks are rejected (`block_overlap`): edit the existing block instead. | Allow stacking. |
| B11 | 10 | Images: JPG/PNG/WebP up to 5 MB (checked by file content), max 10 per facility. The first image is the cover. `imageUrls` in the facility save is still accepted; `null` keeps the current images. | — |
| B12 | 11 | Geocoding runs through the backend (`GET /api/geocoding/search?q=`) so the provider key stays secret. The mock only knows 12 Kosovo cities. | Frontend calls the provider directly (exposes the key). |

## Reservations

| # | Story | Decision | Alternative |
|---|---|---|---|
| R1 | 30 | MVP reservations are **Confirmed immediately** (no customer payment). | Manual confirmation by the business. |
| R2 | 31 | Double booking is blocked by a PostgreSQL exclusion constraint over `[start, end + buffer)`. Duplicate submits use the **`Idempotency-Key` header** (replay returns the same reservation). | — |
| R3 | 32 | Limits (editable in admin settings): **max 2 per customer per day** (cancelled don't count) and **max 2 upcoming**. Guests: the same limits per phone number and per device, plus 10 attempts/hour per IP/device and an IP rate limit. Device and IP are stored only as hashes. | **Confirm the numbers.** |
| R4 | 33 | Cancellation deadline: facility value, else business default (24 h). The same deadline applies to changing date/time. No refunds in MVP. | Separate modify deadline. |
| R5 | 34 | Rescheduling is the same facility, sport and duration. **The price is re-quoted for the new time** (snapshot updated and recorded in history). | **Keep the original price.** |
| R6 | 35 | Statuses: Confirmed, CancelledByCustomer, CancelledByBusiness, Completed, NoShow (+ Pending/Expired only for post-MVP online payment). **Nothing is auto-completed**: the business marks Completed/No-show after the start (they can correct one to the other). | Auto-complete N hours after the end. |
| R7 | 37 | Guests get a secret link token (only its hash is stored). Retrying with the same Idempotency-Key gives a **new** token (the old link stops working). Guests can view and cancel, not reschedule. | Allow guest reschedule. |
| R8 | 20 | External reservations ignore opening hours and the booking window (the business knows best) but can't overlap reservations or blocks. The price defaults to the facility price, or the business enters one. | — |
| R9 | 38 | Business emails: new SportX reservation, customer cancellation/change, business cancellation/change. Sent in the business's language (sq/en templates; tr/sr fall back to English for now). | **Who should get them: business email only, or staff too?** |
| R10 | 36 | "Past" = ended or Completed/NoShow. Nothing is ever deleted. | — |

## Subscriptions

| # | Story | Decision | Alternative |
|---|---|---|---|
| S1 | 42 | **No plans are seeded in production.** In Development a free "Dev Starter" plan (5 facilities) is created so you can test. | **Decide the plan catalogue and prices** (admin → `POST /api/admin/plans`). |
| S2 | 43 | Activation happens **only via the payment webhook**, never from the frontend redirect. A plan change takes effect when the new plan is paid; the old one is cancelled (no proration). | Proration / switch at renewal. |
| S3 | 44 | Renewal is charged automatically at period end. On failure: PastDue, 14-day grace (setting), a reminder 3 days before the end, then Expired → business Inactive (hidden, no new reservations). Billing stays anchored to the original date, also after late payment. | — |
| S4 | 45 | Confirmed reservations are **never cancelled** by expiry; the business is told by email and on the subscription screen. | — |
| S5 | 46 | After 2 years inactive (setting), business data is **anonymized** (name, contacts, registration no., external customers' contact details); reservation counts stay for statistics. | **Hard delete? Confirm legal retention requirements.** |
| S6 | 43 | "Cancel subscription" = ends at period end, no refund. | Immediate cancel with refund. |

## Platform / admin

| # | Story | Decision | Alternative |
|---|---|---|---|
| P1 | 47 | Admin **and** SuperAdmin can operate the dashboard; **plans, feature flags, platform settings and role changes are SuperAdmin-only**. | **Story says super administrators only: restrict everything to SuperAdmin?** |
| P2 | 48 | Admin cancellations go through the same status rules, with a required reason, and are audited. Suspending a user signs them out everywhere. SuperAdmins can't be suspended. | — |
| P3 | 51 | Catalogue names are translated server-side by `Accept-Language` (sq default, en, tr, sr). All other UI text is translated in the frontend using the `code` of each error. Timestamps are UTC (`Z`); `localStartsAt`-style fields are Kosovo wall-clock time (DST handled). | Server-side translated error messages. |
| P4 | 52 | Login lockout after 5 wrong passwords (15 min, `account_locked`), per-IP rate limits, security headers, data export (`GET /api/account/export`). **Still open: privacy policy text, legal basis, backups, encryption at rest (hosting decision).** | — |
| P5 | 49 | Feature flags start **off** for every post-MVP feature; `guest_booking` is on. `GET /api/feature-flags` gives the frontend the map. | — |

## Post-MVP features (built, behind feature flags, all OFF)

| Story | Flag | Notes |
|---|---|---|
| 57 Staff | `staff_invitations` | Email invitation (7 days), permission flags, resend/revoke, deactivate. Only owners can grant "ManageStaff". The invitation only works for the invited email. |
| 58 Multiple businesses | `multiple_businesses` | **The story says it needs product approval first.** The model supports it; creating a 2nd business is blocked until the flag is on. |
| 59 Venues | `venues` | Optional parent venue; courts stay individually bookable. |
| 60 Customer emails | `customer_email_notifications` | Confirmation/cancel/change copies (sq/en). Needs consent text + a deliverability review. |
| 61 SMS | `sms_notifications`, `sms_verification` | Verification is risk-triggered (recent no-show or a busy device). |
| 62 Customer payments | `customer_payments` + `Facility.RequiresOnlinePayment` | Pending + 15-min hold, webhook confirms, full refund on allowed cancellation. **Refund / cancellation-fee policy needed.** |
| 63 Reviews | `reviews` | Completed reservations only, within 30 days, one per reservation; reports + moderation; business reply. |
| 64 Player profiles | `player_profiles` | Visibility Private/Members/Public; never shows email/phone. |
| 65 Teams | `teams` | Invite/request/approve, captain hand-over, reports. **Safety/moderation rules to define.** |
| 66 Recommendations / promoted | `recommendations`, `promoted_listings` | Transparent scoring (each result lists its reasons). Promotions are admin-created and always labelled "Sponsored", shown separately. |

## Found during the gap review (open)

| # | Topic | Current behaviour | Question |
|---|---|---|---|
| G1 | **Closed accounts (deleted > 60 days ago)** | Stay in the database with their personal data (name, email, phone); only login is blocked. | **Anonymize them after the 60-day window (frees the email for re-registration, better for privacy law)?** |
| G2 | List performance | Reservation lists load each row's facility/sport separately (a few queries per row; fine for pages of 20–50). Search loads up to 300 matching facilities and computes slots in memory. | OK for launch; optimize together with load testing (story 55). |
| G3 | Image files | Removing images or deleting a facility now also deletes the uploaded files (local mock storage). Files from facilities anonymized by retention are kept. | Delete images on retention too, once real storage is chosen? |
| G4 | Hosting | A Dockerfile and a CI build check exist (`.github/workflows/build.yml`: compile + "migrations match the model"). | Choose hosting (and backups) so pre-prod/prod deployment can be added. |
| G5 | Health | `GET /health` reports the API + database status (no auth). | Which uptime monitor should call it? |

## Not done (need a team, infrastructure or decisions)

- **01 Environments & pipeline**: CI build check and Dockerfile added; actual pre-prod/prod environments, deployment and rollback procedure need the hosting choice.
- **53 UI states**: frontend work. The backend gives a stable `code` + safe `message` for every error.
- **54 Logging/monitoring/alerting**: needs a tool choice (e.g. Sentry, Grafana/Loki, Application Insights). Correlation ids (`traceId`) are already in every error and audit row.
- **55 Performance targets** and **56 QA/production readiness**: need load testing and a QA pass.

## Providers to choose (mocks today)

| Need | Mock | Suggested real options |
|---|---|---|
| Email (business notifications) | Logged to the console | Brevo (EU, generous free tier), Postmark, Amazon SES |
| SMS | Logged | Infobip (best Balkan coverage), Twilio |
| Payments (subscriptions; later reservations) | Dev checkout page `/api/payments/mock/{id}` | **Verify for a Kosovo-registered company**: local bank e-commerce acquiring (ProCredit, Raiffeisen, TEB, NLB via a PSP), Paysera, 2Checkout/Verifone. **Stripe and PayPal payouts don't support Kosovo businesses.** |
| Image storage | `wwwroot/uploads` | Cloudflare R2 (no egress fees), Amazon S3, Cloudinary (resizing included) |
| Maps / geocoding | 12 Kosovo cities | Leaflet + OpenStreetMap tiles (free), Nominatim or Mapbox for geocoding; Google Maps (best address coverage, paid) |
