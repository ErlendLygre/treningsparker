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
| Database | PostgreSQL 16+ (SQLite for local dev and tests; SQL Server also supported via `DatabaseProvider=SqlServer`) |
| Hosting | Docker / docker compose, Scaleway Serverless Containers + Managed PostgreSQL, GitHub Pages (demo) |

```
backend/   .NET API + xUnit integration tests
frontend/  React app (+ nginx config for the container)
data/      shared seed data (30 mock parks)
.github/   CI and deploy (Pages + container images) workflows
```

## Running it

### Everything in Docker (API + PostgreSQL + web)

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

Tests use SQLite by default. Set `TEST_POSTGRES` (e.g. `Host=localhost;Port=5432;Username=postgres;Password=postgres`)
to run them against PostgreSQL instead; CI does both.

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

GitHub Pages only serves static files; it can't run the .NET API or the database. The `Deploy` workflow
therefore builds the React app **inside Docker** (`frontend/Dockerfile`, `static` target) and publishes the output to Pages:

- **No `API_URL` variable set** → the site is built in **demo mode**: the API is emulated in the browser with
  `localStorage` (log in as admin with `admin@treningsparker.no` / `Admin123!`). Fine for showing the app off, but data is per browser and not secure.
- **`API_URL` repository variable set** (e.g. `https://treningsparker-api.example.com`) → the Pages site talks to your hosted API.
  Add the Pages origin (`https://<user>.github.io`) to the API's `Cors__Origins__0` setting.

The site is pushed to the `gh-pages` branch, and GitHub normally enables Pages for it automatically. If it doesn't, open
`https://github.com/<owner>/treningsparker/settings/pages` and choose *Deploy from a branch → `gh-pages` / `(root)`*.
The workflow runs on pushes to `main`, or manually.

### Scaleway (production)

The app runs on Scaleway as two **Serverless Containers** (`treningsparker-api` on port 8080 and
`treningsparker-web`, nginx on port 80, which serves the React app and forwards `/api` to the API) plus a
**Managed Database for PostgreSQL**. The `scaleway` job in the Deploy workflow pushes both images to
**Scaleway Container Registry** and redeploys the containers on every push to `main`.

One-time setup, in this order:

1. **API key.** Scaleway console → *IAM* → create an application (e.g. `github-deploy`) with a policy giving it
   full access to Container Registry and Serverless Containers in your project, then generate an API key for it.
   Keep the secret key.
2. **Registry.** *Container Registry* → *Create namespace*, e.g. `treningsparker` in `fr-par`, private.
3. **GitHub.** Repo *Settings → Secrets and variables → Actions*:
   - Secret `SCW_SECRET_KEY` = the secret key from step 1.
   - Variable `SCW_REGISTRY_NAMESPACE` = `treningsparker`. (`SCW_REGION` only if not `fr-par`.)

   Run *Actions → Deploy → Run workflow*. The images are pushed; the deploy step is skipped until step 6.
4. **Database.** *Managed Databases → PostgreSQL* → create an instance in the same region (PostgreSQL 16 or newer, the
   smallest node type is plenty). Create a user with a strong password and a database `treningsparker`.
   Serverless Containers have no fixed outgoing IP, so the instance's allowed IPs must include `0.0.0.0/0`
   (the connection is TLS + password protected) unless you connect the containers through a Private Network.
5. **Containers.** *Serverless → Containers* → create a namespace in the same region, then two containers from
   your registry images (tag `latest`):

   | Container | Port | Memory | Environment variables | Secrets |
   | --- | --- | --- | --- | --- |
   | `treningsparker-api` | 8080 | 512 MB | `DatabaseProvider=Postgres`, `Admin__Email=<you>` | `ConnectionStrings__Default=Host=<db-host>;Port=<db-port>;Database=treningsparker;Username=<user>;Password=<password>;SSL Mode=Require`<br>`Jwt__Key=<random, 32+ chars>` (e.g. `openssl rand -base64 48`)<br>`Admin__Password=<strong password>` |
   | `treningsparker-web` | 80 | 128–256 MB | `API_UPSTREAM=https://<api-container-endpoint>` | |

   The API creates the tables, the admin user and the 30 seed parks on first start. With minimum scale 0 the
   first request after idle has a cold start of a few seconds; set the API's minimum scale to 1 to avoid that.
6. **Auto-deploy.** Copy each container's ID and add GitHub variables `SCW_API_CONTAINER_ID` and
   `SCW_WEB_CONTAINER_ID`. From now on every push to `main` deploys the new images.

Open the `treningsparker-web` endpoint (or attach a custom domain to it) and log in with the admin credentials.
The GitHub Pages site keeps running in demo mode alongside it.
