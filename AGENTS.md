# Treningsparker – agent guide

Map of free outdoor exercise parks in Stavanger and Sandnes. Users suggest parks; an admin approves them
before they appear on the public map. See README.md for the full user-facing documentation.

## Stack and layout

- `frontend/` – React 18 + TypeScript + Vite, Leaflet/react-leaflet (OpenStreetMap tiles), React Router 7 (HashRouter).
  - `src/api/httpApi.ts` talks to the .NET API; `src/api/demoApi.ts` is an in-browser fake of the same API used
    for the GitHub Pages demo (`VITE_DEMO_MODE=true`). Both implement `src/api/types.ts` → keep them in sync.
  - `nginx.conf.template` – serves the built app and proxies `/api` to `API_UPSTREAM`.
- `backend/Treningsparker.Api/` – ASP.NET Core 8 minimal API, EF Core, JWT auth.
  - `Endpoints/` (routes, validation, DTOs), `Data/` (entities, DbContext, seeder), `Security/` (startup checks,
    rate limiting, login throttle).
- `backend/Treningsparker.Api.Tests/` – xUnit integration tests via `WebApplicationFactory` (`ApiFactory`).
- `data/parks.seed.json` – 30 **mock** parks, shared by the API seeder and the demo frontend.
- Database: PostgreSQL in production (Scaleway Managed DB), SQLite for local dev and tests; SQL Server still
  selectable via `DatabaseProvider`. Schema is created with `EnsureCreated` (no migrations).
- Hosting: Scaleway Serverless Containers (`treningsparker-api`, `treningsparker-web`) in `nl-ams`, images in
  Scaleway Container Registry; API and DB share a Private Network. GitHub Pages hosts the demo build.

## Commands

```bash
# Backend tests (SQLite) – must pass before committing backend changes
dotnet test backend/Treningsparker.sln
# Same tests against PostgreSQL (CI runs both)
TEST_POSTGRES="Host=localhost;Port=5432;Username=postgres;Password=postgres" dotnet test backend/Treningsparker.sln

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

- New or changed API behaviour gets an integration test in `backend/Treningsparker.Api.Tests/`.
- Validate input server-side (lengths, allowed values) and return `Results.ValidationProblem` (400), never a 500.
  Mirror limits with `maxLength` in forms.
- Keep `demoApi.ts` behaviour matching the real API when you change endpoints.
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

- `main` deploys to production automatically (Deploy workflow → Scaleway + GitHub Pages).
  Push to a branch first and only update `main` once CI (`ci.yml`) is green.
- Never commit secrets. Secrets live in Scaleway container secrets and GitHub Actions secrets/variables.
- Don't change Scaleway resources, GitHub settings, secrets or variables – describe the steps for the owner instead.
- Ask before changing the database schema: there are no migrations, so existing production tables are not
  altered by `EnsureCreated`.
