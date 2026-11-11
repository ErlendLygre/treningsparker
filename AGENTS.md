# Treningsparker – agent guide

Map of free outdoor exercise parks in Stavanger and Sandnes. Users suggest parks; an admin approves them
before they appear on the public map. See README.md for the full user-facing documentation.

## Stack and layout

- `frontend/` – React 19 with the React Compiler (no manual `useMemo`/`useCallback`), TypeScript 6, Vite 8,
  Leaflet/react-leaflet 5 (OpenStreetMap tiles), React Router 8 (`react-router`, HashRouter). Built on Node 26 LTS.
  - `src/api/httpApi.ts` talks to the .NET API (same origin, `/api/...`). Load data with `useApiData`
    (`src/api/useApiData.ts`), which aborts stale requests; forms submit through `useActionState`.
  - `nginx.conf.template` – serves the built app and proxies `/api` to `API_UPSTREAM`.
- `backend/Treningsparker.Api/` – ASP.NET Core 10 minimal API (.NET 10 LTS, pinned in `global.json`), EF Core 10, JWT auth.
  Requests are validated by the built-in minimal API validation (attributes and `IValidatableObject` on the
  request records in `Endpoints/Dtos.cs`); endpoints return `TypedResults` and pass `CancellationToken` to EF Core.
  - `Endpoints/` (routes, validation, DTOs), `Data/` (entities, DbContext, seeder), `Security/` (startup checks,
    rate limiting, login throttle).
- `backend/Treningsparker.Api.Tests/` – xUnit v3 integration tests via `WebApplicationFactory` (`ApiFactory`), run on
  Microsoft Testing Platform (enabled in `global.json`). Pass `TestContext.Current.CancellationToken` to async calls.
- `data/parks.seed.json` – 30 **mock** parks, loaded by the API seeder into an empty database.
- Database: PostgreSQL in production (Scaleway Managed DB), SQLite for local dev and tests
  (`DatabaseProvider`). Schema is created with `EnsureCreated` (no migrations).
- Hosting: Scaleway Serverless Containers (`treningsparker-api`, `treningsparker-web`) in `nl-ams`, images in
  Scaleway Container Registry; API and DB share a Private Network.
- `infra/` – OpenTofu config for all Scaleway resources (state in a private Object Storage bucket).
  `.github/workflows/infra.yml` validates on push; import/plan/apply only run when started manually.

## Live URLs

- Website (production): https://erlendcorptreningspaf8b8c8ea-treningsparker-web.functions.fnc.nl-ams.scw.cloud
- API (production): https://erlendcorptreningspaf8b8c8ea-treningsparker-api.functions.fnc.nl-ams.scw.cloud
  – health checks: `/api/health` (API only) and `/api/health/db` (database round trip)

## Commands

```bash
# Backend tests (SQLite) – must pass before committing backend changes
dotnet test --solution backend/Treningsparker.slnx
# Same tests against PostgreSQL (CI runs both)
TEST_POSTGRES="Host=localhost;Port=5432;Username=postgres;Password=postgres" dotnet test --solution backend/Treningsparker.slnx

# Frontend – lint and build must pass before committing frontend changes
cd frontend && npm ci && npm run lint && npm run build

# Run locally: API on :5080 (Development → SQLite, default admin admin@treningsparker.no / Admin123!)
cd backend/Treningsparker.Api && dotnet run
cd frontend && npm run dev          # :5173, proxies /api to :5080

# Whole stack in Docker (PostgreSQL + API + nginx) on :8080
docker compose up --build
```

## Domain rules

- Only **Stavanger** and **Sandnes**. New parks must lie inside lat 58.70–59.10, lon 5.45–6.10.
- Park types: `Tuftepark`, `Calisthenicspark`, `Treningspark`, `Hinderløype`. Lists are duplicated in
  `ParkEndpoints.cs` and `frontend/src/types.ts` – change both.
- Status: `Pending` (0) → `Approved` (1) or `Rejected` (2, optional reason). Only approved parks are public;
  users see their own suggestions under "Mine forslag"; admins see everything.
- Equipment is stored as one semicolon-separated string; the API strips `;` from items.
- Admin is a flag on `User`; the first admin is created at startup from `Admin__Email` / `Admin__Password`.
- **All user-facing text is Norwegian Bokmål**, including API error messages. Code, comments and docs are English.
- The seed parks are fictional; never present them as real data.

## Conventions

- Start new dependencies, SDKs, base images and GitHub Actions on their current stable version (for .NET: the
  current LTS). Check the registry instead of relying on memory, and upgrade majors deliberately in their own change.

- New or changed API behaviour gets an integration test in `backend/Treningsparker.Api.Tests/`.
- Validate input server-side (lengths, allowed values) and return `Results.ValidationProblem` (400), never a 500.
  Mirror limits with `maxLength` in forms.
- Match the surrounding code style; keep comments sparse and explain *why*.
- Commit messages: imperative subject, short body explaining why.

## Pitfalls (learned the hard way)

- PostgreSQL names are PascalCase and must be quoted: `SELECT * FROM "Parks" WHERE "Status" = 0`.
- Scaleway pauses idle containers, which kills pooled DB connections → Npgsql pooling is off by default
  (`PostgresConnectionString` in `Program.cs`). Don't re-enable it.
- nginx → API on Scaleway needs `proxy_http_version 1.1` (else 426), `resolver … ipv6=off` and a variable
  `proxy_pass` (the endpoints publish unreachable IPv6 addresses). Keep these in `nginx.conf.template`.
- The API Dockerfile publishes with ReadyToRun; the restore step must also pass `-p:PublishReadyToRun=true`.
  A local publish can succeed from a warm NuGet cache while the clean Docker build fails – CI's `docker` job
  is the real check.
- Outside Development the API **refuses to start** if `Jwt__Key` (32+ chars) or `Admin__Password` is missing or
  a public default. Tests set their own values in `ApiFactory.Settings`; compose uses `AllowInsecureDefaults`.
- The smallest Scaleway container tier (~0.07 vCPU) makes .NET cold starts and logins very slow.
- Rate limiters are in memory and per instance; tests raise the limits via `ApiFactory.Settings`.

## Workflow and boundaries

- `main` deploys to production automatically (Deploy workflow → Scaleway).
  Push to a branch first and only update `main` once CI (`ci.yml`) is green.
- Never commit secrets. Secrets live in Scaleway container secrets and GitHub Actions secrets/variables.
- Change Scaleway resources only through `infra/` on a branch; the owner runs plan/apply in the Infra workflow.
  Never run `tofu apply` yourself or change resources in the console. GitHub settings, secrets and variables are
  the owner's – describe the steps instead.
- Never weaken `prevent_destroy` on the database, and treat any plan that replaces (`-/+`) the database,
  Private Network or containers as a stop-and-ask.
- Ask before changing the database schema: there are no migrations, so existing production tables are not
  altered by `EnsureCreated`.
