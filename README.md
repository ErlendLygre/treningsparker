# Treningsparker – Stavanger & Sandnes

A map of free outdoor exercise parks (Tufteparker, calisthenics parks, outdoor gyms and obstacle courses) in Stavanger and Sandnes.

- **Public map**: every approved park, with search, filters (city, type, lighting), details and directions.
- **Users** can register, log in and suggest new parks by clicking their location on the map.
- **Admins** approve, reject (with a reason) or delete suggestions. A park only shows up on the public map after it is approved.

> **Mock data:** no open dataset of outdoor exercise parks for these municipalities was available, so the app ships
> with 30 *fictional* parks (`data/parks.seed.json`) placed in real neighbourhoods. Replace the file with real data when you have it.

## Stack

| Part | Tech |
| --- | --- |
| Frontend | React 18, TypeScript, Vite, Leaflet/react-leaflet, OpenStreetMap tiles |
| Backend | ASP.NET Core 8 minimal API, EF Core, JWT auth, ASP.NET Identity password hashing |
| Database | SQL Server 2022 (SQLite for local dev and tests) |
| Hosting | Docker / docker compose, GitHub Pages (frontend), GHCR images |

```
backend/   .NET API + xUnit integration tests
frontend/  React app (+ nginx config for the container)
data/      shared seed data (30 mock parks)
.github/   CI and deploy (Pages + container images) workflows
```

## Running it

### Everything in Docker (API + SQL Server + web)

```bash
docker compose up --build
```

Open http://localhost:8080. Log in as admin with `admin@treningsparker.no` / `Admin123!`.
Override `DB_PASSWORD`, `JWT_KEY`, `ADMIN_EMAIL` and `ADMIN_PASSWORD` via environment variables or a `.env` file in anything but local dev.

### Without Docker

```bash
cd backend/Treningsparker.Api && dotnet run     # http://localhost:5080, SQLite in Development
cd frontend && npm install && npm run dev       # http://localhost:5173, proxies /api to the API
```

### Tests

```bash
dotnet test backend/Treningsparker.sln
cd frontend && npm run lint && npm run build
```

## API

| Method | Path | Access |
| --- | --- | --- |
| `POST` | `/api/auth/register`, `/api/auth/login` | public |
| `GET` | `/api/parks` | public (approved only) |
| `POST` | `/api/parks` | logged in, creates a *pending* park |
| `GET` | `/api/parks/mine` | logged in |
| `GET` | `/api/admin/parks?status=pending` | admin |
| `POST` | `/api/admin/parks/{id}/approve`, `/api/admin/parks/{id}/reject` | admin |
| `DELETE` | `/api/admin/parks/{id}` | admin |

## Deployment

### GitHub Pages (frontend)

GitHub Pages only serves static files; it can't run the .NET API or SQL Server containers. The `Deploy` workflow
therefore builds the React app **inside Docker** (`frontend/Dockerfile`, `static` target) and publishes the output to Pages:

- **No `API_URL` variable set** → the site is built in **demo mode**: the API is emulated in the browser with
  `localStorage` (log in as admin with `admin@treningsparker.no` / `Admin123!`). Fine for showing the app off, but data is per browser and not secure.
- **`API_URL` repository variable set** (e.g. `https://treningsparker-api.example.com`) → the Pages site talks to your hosted API.
  Add the Pages origin (`https://<user>.github.io`) to the API's `Cors__Origins__0` setting.

The site is pushed to the `gh-pages` branch, and GitHub normally enables Pages for it automatically. If it doesn't, open
`https://github.com/<owner>/treningsparker/settings/pages` and choose *Deploy from a branch → `gh-pages` / `(root)`*.
The workflow runs on pushes to `main`, or manually.

### Container images

The same workflow pushes `ghcr.io/<owner>/treningsparker-api` and `ghcr.io/<owner>/treningsparker-web` to GitHub Container Registry.

### Where to run the containers

| Option | Good fit because | Database |
| --- | --- | --- |
| **Azure Container Apps** (recommended) | First-class .NET/SQL Server story, scales to zero, pulls straight from GHCR | Azure SQL Database (free offer / serverless tier) |
| Azure App Service for Containers | Simple "one web app per container", easy custom domains | Azure SQL Database |
| Google Cloud Run | Scales to zero, generous free tier | Cloud SQL for SQL Server, or switch EF provider to PostgreSQL |
| Fly.io / Render / Railway | Very quick to start, can run `docker-compose`-like setups | Managed Postgres (switch EF provider) or a SQL Server container with a volume |
| Any VM (Azure VM, Hetzner, DigitalOcean…) | Run `docker compose up -d` as-is | SQL Server container from `docker-compose.yml` |

The API reads all settings from environment variables (`ConnectionStrings__Default`, `Jwt__Key`, `Admin__Email`,
`Admin__Password`, `Cors__Origins__0`), so it runs unchanged on any of these.
