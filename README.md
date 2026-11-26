# SportX (FiveAndOne) – Backend API

Multi-sport facility booking platform for Kosovo. ASP.NET Core 9 Web API + PostgreSQL (EF Core).

| Project | Contents |
|---|---|
| `FiveAndOne` (API) | Controllers, Program.cs, error handling, mock providers, background workers |
| `FiveAndOne.Application` | Business logic (services), DTOs |
| `FiveAndOne.Domain` | Entities and enums |
| `FiveAndOne.Infrastructure` | EF Core DbContext, configurations, migrations |

Docs: [frontend API guide](docs/FRONTEND_API.md) · [open questions & decisions](docs/OPEN_QUESTIONS.md) · [end-to-end test](tests/e2e/README.md)

## Run locally

Requirements: .NET 9 SDK, PostgreSQL 13+ (the connection string is in `FiveAndOne/appsettings.json`).

1. One-time secrets (kept out of source control):
   ```powershell
   dotnet user-secrets set "Jwt:Key" "<any random string of 64+ characters>" --project FiveAndOne
   dotnet user-secrets set "SuperAdmin:Password" "<password for superadmin@fiveandone.local>" --project FiveAndOne
   ```
2. Create / update the database:
   ```powershell
   dotnet tool restore
   dotnet ef database update --project FiveAndOne.Infrastructure --startup-project FiveAndOne
   ```
3. Run (Visual Studio F5, or):
   ```powershell
   dotnet run --project FiveAndOne
   ```
   Swagger opens at `https://localhost:7141/swagger`. Health check: `GET /health`.

In Development a free "Dev Starter" subscription plan is created, and payments use a mock checkout page (Pay / Fail buttons) so the full flow works without a real provider.

## Configuration

| Section | Purpose |
|---|---|
| `ConnectionStrings:DefaultConnection` | PostgreSQL |
| `Jwt` | Issuer, audience, **Key (secret, ≥ 32 bytes)**, token lifetimes |
| `SuperAdmin` | First SuperAdmin, created at startup if none exists (password = secret) |
| `App` | Frontend/API base URLs (links in emails, checkout), time zone |
| `Cors:AllowedOrigins` | Frontend origins |
| `Facilities` | Image limits, drafts allowed without a subscription |
| `Payments` | Webhook secret, mock behaviour, payment hold minutes |
| `RateLimiting` | Per-IP limits per minute (auth, guest bookings, verification, all requests) |

In production set them as environment variables (e.g. `Jwt__Key`, `ConnectionStrings__DefaultConnection`).
Booking limits, grace days, retention and the review window are platform settings, editable by a SuperAdmin at runtime (`/api/admin/settings`).

## External providers

Email, SMS, payments, file storage and geocoding are **mocks** (see `FiveAndOne/Providers/MockProviders.cs`), registered in `Program.cs`. Replace each registration with a real implementation of the same interface (`FiveAndOne.Application/Common/Abstractions/Providers.cs`). Suggestions are in [docs/OPEN_QUESTIONS.md](docs/OPEN_QUESTIONS.md).

## Background jobs

They run inside the API: notification outbox (every 15 s), unpaid reservation expiry (every minute), and subscription renewals/grace/expiry plus data retention (hourly). A SuperAdmin can run one immediately: `POST /api/admin/jobs/{subscriptions|retention|notifications|reservation-expiry}/run`.

## Docker

```powershell
docker build -t sportx-api .
docker run -p 8080:8080 -e ConnectionStrings__DefaultConnection="Host=...;Database=...;Username=...;Password=..." -e Jwt__Key="<secret>" -v sportx-uploads:/app/wwwroot/uploads sportx-api
```
Apply migrations against the target database before starting a new version (`dotnet ef database update` or an EF migration bundle).
