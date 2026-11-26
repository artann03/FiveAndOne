# End-to-end API test

`e2e.mjs` drives the whole API like real users would: about 340 checks covering every endpoint, from business signup, subscription and facility setup to bookings (customer, guest, concurrent), business and admin management, every post-MVP feature (it switches the flags on and restores them) and the subscription jobs. Test data uses `e2e_…` accounts and `E2E …` names; `cleanup.sql` removes it and resets flags/settings.

**Use a local development database only**: the script edits rows directly to simulate time passing (expired subscriptions, past reservations).

Requirements: Node 18+, `psql` (PostgreSQL bin folder), the dev SuperAdmin.

1. Start the API on port 5299 with relaxed rate limits:
   ```powershell
   $env:ASPNETCORE_ENVIRONMENT='Development'; $env:RateLimiting__AuthPerMinute='5000'; $env:RateLimiting__GuestBookingsPerMinute='5000'; $env:RateLimiting__VerificationPer10Minutes='5000'; $env:RateLimiting__RequestsPerMinute='100000'; $env:App__ApiBaseUrl='http://localhost:5299'; dotnet run --project FiveAndOne --no-launch-profile --urls http://localhost:5299
   ```
2. Run the test:
   ```powershell
   node tests/e2e/e2e.mjs
   ```
3. Clean up:
   ```powershell
   psql -h localhost -U postgres -d FiveAndOne -f tests/e2e/cleanup.sql
   ```

Optional environment variables: `API_URL`, `PGPASSWORD`, `SUPERADMIN_EMAIL`, `SUPERADMIN_PASSWORD`.
