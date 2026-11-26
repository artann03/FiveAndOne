# SportX API – guide for the frontend

Swagger: `https://localhost:7141/swagger` (Development). Every request/response shape is there; this page covers the rules and the endpoint map.

## Conventions

| Topic | Rule |
|---|---|
| Auth | `Authorization: Bearer <accessToken>`. Access token 15 min, refresh token 7 days, **single-use**: always store the new `refreshToken` from `/api/auth/refresh` and let only one refresh run at a time. |
| Business context | Business endpoints act on the user's business. If `GET /api/business/mine` returns more than one, send **`X-Business-Id: <id>`** on every business request. |
| Language | Send `Accept-Language: sq` (default), `en`, `tr` or `sr`. Sport/amenity names come back translated. All other UI text is translated in the frontend. |
| Dates/times | Instants are UTC ISO strings ending in `Z` (`startsAt`, `endsAt`, `createdAt`). `local…` fields (`localStartsAt`) are Kosovo wall-clock time without an offset: display those. Dates: `"2026-11-28"`. Times: `"18:30"`. |
| Enums | Strings, e.g. `"Confirmed"`, `"Monday"`, `"PerHour"`. |
| Errors | Always `{ status, code, message, errors?, details?, traceId }`. Use **`code`** for logic and translation; `message` is safe to show. `errors` = field → messages (forms). `details` = extra data (e.g. `details.conflicts`). |
| Paging | `?page=1&pageSize=20` → `{ items, page, pageSize, totalCount, totalPages }`. |
| Booking headers | `Idempotency-Key: <uuid per submit attempt>` (reuse it when retrying the same submit) and `X-Device-Id: <random id kept in localStorage>`. |
| Feature flags | `GET /api/feature-flags` → `{ "reviews": false, ... }`. Hide post-MVP UI when its flag is false (the API returns 404 `feature_disabled` anyway). |
| Unknown routes | Any unknown `/api/...` path returns 404 `not_found` (never 401), so a 401 always means "token missing/expired". |
| Health | `GET /health` → `Healthy` / `Unhealthy` (API + database). |

## Changes to endpoints that already existed

- `/api/business/profile`: same body; the response now has `id`, `status`, `hasRegistrationNumber` and cancellation fields. Languages now also accept `tr`.
- `/api/business/facilities`:
  - Responses have `businessId` instead of `ownerUserId`.
  - Sports now include rules (`effectiveDurationOptions` and others).
  - `imageUrls` in the save body is **optional** (`null` keeps the images).
  - **Publishing now also needs weekly hours + at least 1 price + an active subscription.** `missingForPublish` lists `operatingHours` / `pricing`.
- `/api/business/facilities/usage`: now includes `hasActiveSubscription`, `planName` and `canPublish`.
- Public `/api/facilities` (search) and `/api/facilities/{idOrSlug}`: new response shapes (cards / full details). See below.
- Removed the 1:1 "Business role = one business" assumption. A business account registered with the old `/api/auth/register` still works: its first `PUT /api/business/profile` creates the business.

## Endpoint map by screen

### Auth & account
| Screen / action | Endpoint |
|---|---|
| Sign up (customer) | `POST /api/auth/register` (`phoneNumber`, `preferredLanguage` optional) |
| **Create business account** | `POST /api/auth/register-business` (business + owner in one step, logs in) |
| Log in / refresh / log out | `POST /api/auth/login`, `/refresh`, `/logout`, `/logout-all` |
| Me | `GET /api/auth/me` |
| Profile page ("Save profile") | `GET/PUT /api/account/profile` |
| Change password | `PUT /api/auth/password` |
| Deactivate account | `DELETE /api/auth/me` (body `{password}`), restore within 60 days: `POST /api/auth/reactivate` |
| Download my data | `GET /api/account/export` |

### Public (no login)
| Screen | Endpoint |
|---|---|
| Homepage | `GET /api/home` (sports with counts, cities, featured, sponsored) |
| Sports tiles / one sport | `GET /api/sports?includeCounts=true`, `GET /api/sports/{idOrSlug}` |
| Cities / amenities | `GET /api/cities`, `GET /api/amenities` |
| Search results + filters + sort | `GET /api/facilities?sport=futsal&city=Prishtinë&date=2026-11-28&time=18:00&durationMinutes=60&minPrice=&maxPrice=&availableOnly=true&latitude=&longitude=&radiusKm=&sort=relevance|price_asc|distance|availability&page=` |
| Map | `GET /api/facilities/map?minLat=&maxLat=&minLng=&maxLng=&sport=` |
| Facility details | `GET /api/facilities/{idOrSlug}` (hours, prices, cancellation policy, sports + durations) |
| Calendar strip | `GET /api/facilities/{idOrSlug}/calendar?sportId=&from=2026-11-28&days=14&durationMinutes=` |
| Time buttons | `GET /api/facilities/{idOrSlug}/availability?sportId=&date=&durationMinutes=` → `slots[]` with `available`, `unavailableReason`, `price` |
| Plans (business pricing page) | `GET /api/plans` |

Search cards include up to 3 `nextSlots` (for the date if given, else the next 7 days). They are indicative; the backend revalidates at booking.

### Booking (customer or guest)
| Action | Endpoint |
|---|---|
| **Confirm reservation** | `POST /api/reservations` with headers `Idempotency-Key`, `X-Device-Id`. Body `{facilityId, sportId, startsAt (slot's UTC startsAt), durationMinutes, notes, guest?: {name, phone, email?}}`. Logged in = no `guest`. |
| Confirmation page | Response: `bookingNumber`, `status`, price snapshot, times, `cancellationPolicy`. **Guests also get `guestAccessToken` + `guestAccessUrl`: show "Save this reference to access your reservation later."** |
| My reservations | `GET /api/reservations?scope=upcoming|past|cancelled|all` |
| Detail / cancel / change | `GET /api/reservations/{id}`, `POST /{id}/cancel {reason}`, `PUT /{id}/reschedule {startsAt}` |
| Guest retrieval page | `GET /api/reservations/guest/{token}`, cancel `POST /api/reservations/guest/{token}/cancel` |

Booking error codes to handle:
- `slot_unavailable` (show the message: "This time slot is no longer available. Please choose another time.")
- `invalid_slot`, `too_soon`, `too_far_ahead`, `pricing_missing`, `facility_unavailable`
- `daily_limit_reached`, `active_limit_reached`
- `guest_details_required`, `guest_rate_limited` (429), `phone_verification_required` (only if SMS verification is on)
- `cancellation_deadline_passed`, `invalid_status`, `idempotency_key_reused`

### Business dashboard
| Menu | Endpoints |
|---|---|
| Business switcher | `GET /api/business/mine` |
| Overview | `GET /api/business/dashboard` (upcoming reservations, counts, subscription summary, `canAddFacility`, `canTakeReservations`) |
| Facilities | `GET/POST /api/business/facilities`, `GET/PUT/DELETE /{id}`, `POST /{id}/publish`, `POST /{id}/hide`, `GET /usage` |
| Images | `POST /{id}/images` (multipart, field `files`), `DELETE /{id}/images/{imageId}`, `PUT /{id}/images/order` (array of ids) |
| Configuration (one call) | `GET /api/business/facilities/{id}/configuration` |
| Sports & Pricing | `PUT /{id}/sports`, `GET/PUT /{id}/pricing`, `GET /{id}/pricing/preview?sportId&date&startTime&durationMinutes` |
| Durations / buffer / cancellation | `PUT /{id}/rules` (also `requiresOnlinePayment`, only used when the `customer_payments` flag is on) |
| Availability: weekly hours | `GET /{id}/hours?sportId=`, `PUT /{id}/hours` (days with `isClosed` + `windows`; `summary` returned), `DELETE /{id}/hours/{sportId}` |
| Availability: exceptions / closures | `GET/POST /{id}/exceptions`, `DELETE /{id}/exceptions/{exceptionId}` |
| Availability: block time | `GET /{id}/blocks`, `GET /api/business/blocks?facilityId&from&to`, `POST /{id}/blocks`, `DELETE /{id}/blocks/{blockId}` |
| Preview slots | `GET /{id}/availability?sportId&date&durationMinutes` |
| Reservations | `GET /api/business/reservations?facilityId&sportId&from&to&status&source=SportX|External&search&scope=upcoming|past|all` |
| Reservation detail + actions | `GET /{id}` (incl. history), `POST /{id}/cancel {reason}`, `POST /{id}/complete`, `POST /{id}/no-show`, `PUT /{id}/reschedule {startsAt, durationMinutes?}`, `PUT /{id}/notes` |
| Add external reservation | `POST /api/business/reservations/external`, edit `PUT /{id}/external` |
| Analytics | `GET /api/business/analytics?from&to&facilityId` |
| Business profile | `GET/PUT /api/business/profile`, `PUT /api/business/cancellation-policy` |
| Subscription | `GET /api/business/subscription`, `POST /checkout {planId}` → redirect to `checkoutUrl`, then poll `GET` until `Active`. `POST /pay` ("Pay now"), `POST /cancel`, `POST /resume`. |

**Conflict flow (closures, blocks):** a `409` with `code: "reservation_conflicts"` comes with `details.conflicts` (reservation list). Show them and resend the same body with `conflictResolution: "CancelReservations"` or `"KeepReservations"`. External reservations/blocks that overlap return `slot_unavailable` / `block_overlap` with details.

**Local mock payments (Development):** the `checkoutUrl` opens a small page with Pay / Fail buttons.

Business error codes:
- `no_business`, `business_selection_required`, `missing_permission`, `business_suspended`, `business_not_active`
- `subscription_required`, `plan_limit_reached`, `plan_too_small`, `subscription_expired`
- `facility_incomplete` (`errors` lists the fields), `facility_suspended`, `facility_has_bookings`
- `invalid_duration`, `reservation_conflicts`, `block_overlap`
- `reservation_not_started`, `reservation_ended`, `invalid_status_transition`, `not_external`
- `image_too_large`, `invalid_image`, `too_many_images`, `last_image`

### Super admin
| Menu | Endpoints |
|---|---|
| Dashboard / analytics / audit | `GET /api/admin/dashboard`, `/analytics?from&to`, `/audit-logs?action&entityType&entityId&actorUserId&businessId&from&to` |
| Customers | `GET /api/admin/users?role&status&search`, `GET /{id}`, `POST /{id}/suspend {reason}`, `/activate`, `PUT /{id}/role` (SuperAdmin) |
| Businesses | `GET /api/admin/businesses?status&search`, `GET /{id}`, `POST /{id}/suspend {reason}`, `/unsuspend` |
| Facilities | `GET /api/admin/facilities?status&businessId&search`, `GET /{id}`, `POST /{id}/suspend`, `/unsuspend`, `/hide` |
| Reservations | `GET /api/admin/reservations?businessId&...`, `GET /{id}`, `POST /{id}/cancel {reason}` |
| Sports / categories / amenities | `GET/POST /api/admin/catalog/sports`, `PUT /sports/{id}` (incl. `nameTranslations`, `isActive`), `GET/POST/PUT/DELETE /api/admin/catalog/amenities` |
| Subscription plans | `GET /api/admin/plans`, `POST`, `PUT /{id}` (SuperAdmin) |
| Business subscriptions / retention | `GET /api/admin/subscriptions?status`, `GET /api/admin/retention` |
| Feature flags | `GET /api/admin/feature-flags`, `POST`, `POST /{key}/enable`, `POST /{key}/disable` (SuperAdmin) |
| Platform settings | `GET /api/admin/settings`, `PUT /api/admin/settings/{key} {value}` (SuperAdmin) |
| Run a background job now | `POST /api/admin/jobs/{subscriptions\|retention\|notifications\|reservation-expiry}/run` (SuperAdmin) |
| Moderation / promotions | `GET /api/admin/reports`, `POST /reports/{id}/resolve`, `PUT /api/admin/reviews/{id}/status`, `GET/POST/PUT/DELETE /api/admin/promotions` |

### Post-MVP (check the feature flag first)
| Feature | Endpoints |
|---|---|
| Staff (`staff_invitations`) | `GET /api/business/staff`, `PUT /api/business/staff/{userId}`, `GET/POST /api/business/staff/invitations`, `POST /invitations/{id}/resend`, `DELETE /invitations/{id}`, accept: `POST /api/invitations/accept {token}` (link: `/invitations/accept?token=`) |
| Multiple businesses (`multiple_businesses`) | `POST /api/business` + `X-Business-Id` header |
| Venues (`venues`) | `GET/POST/PUT/DELETE /api/business/venues`, public `GET /api/venues/{idOrSlug}` |
| Reviews (`reviews`) | `GET /api/facilities/{idOrSlug}/reviews`, `POST /api/reviews {bookingId, rating, comment}` (when the reservation's `canReview` is true), `DELETE /api/reviews/{id}`, `POST /api/reports/{Review|User|Team}/{id} {reason}`, business reply `PUT /api/business/reviews/{id}/reply` |
| Player profiles (`player_profiles`) | `GET/PUT /api/players/me`, `GET /api/players?sportId&city&level`, `GET /api/players/{username}` |
| Teams (`teams`) | `GET /api/teams`, `/mine`, `/{id}`, `POST`, `PUT/DELETE /{id}`, `POST /{id}/invitations {username}`, `POST /{id}/invitations/respond?accept=`, `POST /{id}/requests`, `POST /{id}/requests/{userId}?approve=`, `DELETE /{id}/members/{userId}`, `POST /{id}/leave` |
| SMS verification (`sms_verification`) | `POST /api/verification/phone/send {phone}`, `POST /verify {phone, code}` → send `phoneVerificationToken` with the booking |
| Recommendations (`recommendations`) | `GET /api/recommendations` (each item has `reasons[]`) |
| Address search (for the map pin) | `GET /api/geocoding/search?q=` (logged in) |

## Enum values

- **BookingStatus**: Pending, Confirmed, CancelledByCustomer, CancelledByBusiness, Completed, NoShow, Expired
- **BookingSource**: SportX, External
- **ExternalChannel**: Phone, WhatsApp, SocialMedia, WalkIn, Other
- **PaymentStatus**: NotRequired, Pending, Paid, Refunded, Failed
- **FacilityStatus**: Draft, Published, Hidden, Suspended
- **BusinessStatus**: Active, Suspended, Inactive
- **FacilityType**: Court, Pitch, Field, Lane, Course, Range, Pool, Room, Other
- **PriceType**: PerReservation, PerHour
- **BlockReason**: Maintenance, PrivateUse, Event, Other
- **ConflictResolution**: CancelReservations, KeepReservations
- **SubscriptionStatus**: Incomplete, Active, PastDue, Expired, Cancelled
- **BusinessPermissions** (flags): ManageReservations, ManageAvailability, ManageFacilities, ManagePricing, ViewAnalytics, ManageProfile, ManageStaff, ManageSubscription, All
- **ProfileVisibility**: Private, Members, Public
- **SkillLevel**: see Swagger
- **TeamMemberStatus**: Invited, Requested, Active, Declined, Left, Removed
