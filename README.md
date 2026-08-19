# Poll & Survey Builder — Backend

Backend for the AMD201 45H Workshop assignment: a real-time poll builder where anyone can create a
multiple-choice question, share a short link, collect votes, and watch the results update live
without refreshing the page.

The backend is **four ASP.NET Core services behind an Ocelot API gateway**, deployed to Render as
four containers, backed by PostgreSQL on Neon. The Vue SPA is deployed separately on Vercel and
talks only to the gateway.

- **Live gateway:** _set after deploy — see [Deployment](#deployment)_
- **Live frontend:** _set after deploy_
- **Frontend repository:** https://github.com/atmos1011/vuetest

---

## Architecture

```
                  browser
                     |
                     v
        +---------------------------+
        |     Vue SPA (Vercel)      |
        +-------------+-------------+
                      |  HTTPS + WebSocket
                      v
        +---------------------------+
        |   ApiGateway (Ocelot)     |   <- the only public entry point
        +----+-----------------+----+
             |                 |
   /api/polls|                 | /api/polls/{code}/vote
   /api/polls/{code}           | /api/polls/{code}/results
             v                 v
    +----------------+  +----------------+       +----------------------+
    |  PollService   |  | VotingService  |------>|   RealtimeService    |
    | polls, options |  |     votes      |       | SignalR hub,  no DB  |
    +--------+-------+  +--------+-------+       +-----------+----------+
             |                   |                           |
             v                   v                           | WebSocket push
    +----------------+  +----------------+                   | (proxied by the gateway)
    | Neon Postgres  |  | Neon Postgres  |                   v
    | schema: polls  |  | schema: voting |            every browser watching
    +----------------+  +----------------+            that poll's results page
```

| Service | Owns | Responsibility |
|---|---|---|
| **ApiGateway** | nothing | Ocelot. Publishes the REST surface the brief specifies and proxies the SignalR WebSocket. One public origin, so the SPA has one URL and one CORS origin to trust. |
| **PollService** | `polls` schema (`Polls`, `PollOptions`) | Create, read, replace (`PUT`), patch (`PATCH`), close, QR code. Owns the creator-token rule. |
| **VotingService** | `voting` schema (`Votes`) | Cast a vote, tally results, CSV export. Owns the one-vote-per-respondent rule. |
| **RealtimeService** | nothing | Hosts the SignalR hub and fans out pushes. No database, so it can be restarted freely. |

### How the services talk to each other

- **VotingService → PollService** — before recording a vote, VotingService fetches the poll
  **through the gateway** (`GET /api/polls/{code}`) to check it exists, is open, and has that
  option. This follows the pattern from the microservices lab: services address each other by the
  one public URL rather than by hardcoded hostnames.
- **VotingService → PollService (internal)** — after the first vote lands it posts to
  `internal/polls/{code}/votes-recorded`, so the creator can no longer rewrite the question under
  people who have already voted.
- **VotingService → RealtimeService** and **PollService → RealtimeService** (internal) — new
  tallies and close events become SignalR pushes.

Internal calls go **direct**, not through the gateway, and carry a shared `X-Internal-Key` secret.
The gateway deliberately publishes no `/internal/*` route, so those endpoints are unreachable from
the internet.

### Design decisions worth explaining

| Decision | Why |
|---|---|
| Gateway paths are `/api/polls/...` even for voting | The brief specifies those exact REST endpoints. The gateway presents them while three services do the work behind it. |
| Votes reference a poll by **code**, not a foreign key | The `Polls` table lives in another service's schema. A cross-service FK would weld the two databases together and defeat the split. |
| One Neon instance, two schemas | Database-per-service in spirit, one free-tier instance in practice — an honest cost trade-off, stated rather than hidden. |
| One-vote-per-respondent is a **unique index** | `(PollCode, VoterToken)`. Application-level "have you voted?" checks lose races; the database does not. |
| Voter identity in `localStorage`, not a cookie | Vercel → Render is cross-site, so a cookie would need `SameSite=None; Secure` and would still be dropped by Safari and Brave. That would break the live demo. |
| Creator token stored **hashed** | Returned once at creation, SHA-256 in the database. A leaked row cannot be used to close someone else's poll. |
| Editing refused once voting starts | Rewriting an option would silently reassign existing votes to different text. |
| Integration tests use SQLite, not EF InMemory | InMemory ignores unique indexes, so the duplicate-vote test would pass without the constraint ever being exercised. |

---

## Running it locally

### Everything in Docker (recommended — matches production)

```bash
docker compose up --build
```

That starts PostgreSQL plus all four services. The gateway is on **http://localhost:5000**, and it
is the only port the SPA should ever call.

To run the same stack against the **real Neon database** instead of the local container, copy
`.env.example` to `.env` and put your connection string in it:

```bash
cp .env.example .env      # then edit POSTGRES_CONNECTION
docker compose up --build
```

Compose picks `.env` up automatically. `.env` is gitignored; `.env.example` is not.

```bash
# create a poll
curl -X POST http://localhost:5000/api/polls \
  -H "Content-Type: application/json" \
  -d '{"question":"Tabs or spaces?","options":["Tabs","Spaces"]}'

# vote (use the code from the response above)
curl -X POST http://localhost:5000/api/polls/<code>/vote \
  -H "Content-Type: application/json" -H "X-Voter-Token: browser-a" \
  -d '{"optionIndex":0}'

# results
curl http://localhost:5000/api/polls/<code>/results
```

### From the IDE

Start PostgreSQL, then run all four projects — `PollBuilder.slnx` has a launch profile for each:

```bash
docker compose up -d postgres
dotnet run --project src/PollService      # http://localhost:5101
dotnet run --project src/VotingService    # http://localhost:5102
dotnet run --project src/RealtimeService  # http://localhost:5103
dotnet run --project src/ApiGateway       # http://localhost:5000
```

Each service serves interactive API docs at `/docs` (Scalar) and a health check at `/health`.

### Tests

```bash
dotnet test PollBuilder.slnx
```

Unit tests for the business rules, integration tests that boot each service over real HTTP against
SQLite, and SignalR tests that connect a real hub client and assert the push arrives.

### Formatting and static analysis

```bash
dotnet format PollBuilder.slnx --verify-no-changes
```

This is exactly what CI runs. Roslyn analyzers are enabled in `Directory.Build.props` and run as
part of every build.

### Database migrations

```bash
dotnet ef migrations add <Name> --project src/PollService   --context PollDbContext   --output-dir Data/Migrations
dotnet ef migrations add <Name> --project src/VotingService --context VotingDbContext --output-dir Data/Migrations
```

Migrations are applied automatically at startup (`Service:ApplyMigrationsOnStartup`), because
Render's Docker deploy has no separate release step.

Each service keeps its migrations ledger **inside its own schema**
(`polls.__EFMigrationsHistory`, `voting.__EFMigrationsHistory`) rather than in the default
`public.__EFMigrationsHistory`. The two services share one Neon database to stay on the free tier,
and a shared ledger would mean two independently deployable services writing to the same table —
exactly the coupling that splitting the schemas is meant to prevent.

---

## Configuration

Every setting is an environment variable in production. `__` is the separator for nested keys.

| Variable | Services | Purpose |
|---|---|---|
| `ConnectionStrings__Postgres` | Poll, Voting | Neon connection string. A `postgresql://` URI is accepted and converted automatically. |
| `Service__AllowedOrigins__0` | all | Browser origin allowed by CORS, e.g. the Vercel URL. `*.vercel.app` preview domains are matched automatically. |
| `Service__ShareBaseUrl` | Poll | SPA origin used to build share links, e.g. `https://myapp.vercel.app`. |
| `Service__ApplyMigrationsOnStartup` | Poll, Voting | `true` in production. |
| `ServiceEndpoints__GatewayBaseUrl` | Voting | Gateway URL, used to look polls up. |
| `ServiceEndpoints__PollServiceBaseUrl` | Voting | PollService URL, for the internal callback. |
| `ServiceEndpoints__RealtimeBaseUrl` | Poll, Voting | RealtimeService URL, for pushes. |
| `ServiceEndpoints__InternalApiKey` | all | Shared secret for internal endpoints. **Must be the same value on all four services.** |
| `Downstream__PollService`, `Downstream__VotingService`, `Downstream__RealtimeService` | Gateway | Where the gateway routes each path. Overrides the placeholder hosts in `ocelot.json`. |
| `PORT` | all | Injected by Render. Defaults to 8080. |

---

## Deployment

The database is **Neon** (free tier, no expiry). The services run on **Render** as four Docker web
services from this one repository; they differ only in Dockerfile path.

### 1. Neon

Create a project, then on the dashboard open the **Connection string** panel, pick the **.NET**
snippet format and keep **Pooled connection** ticked. Choose a region near your Render region —
mismatched regions add noticeable latency to every query during a live demo.

Both formats are accepted: the `.NET` key/value string, and the `postgresql://` URI. The URI form is
converted automatically, including dropping libpq-only parameters such as `channel_binding` that
Npgsql does not understand.

Both services use the same database and create their own schema on first run.

### 2. Render — four web services

For each: **New → Web Service**, point it at this repository, choose **Docker**, set the Dockerfile
path:

| Render service | Dockerfile path |
|---|---|
| `pollbuilder-poll` | `src/PollService/Dockerfile` |
| `pollbuilder-voting` | `src/VotingService/Dockerfile` |
| `pollbuilder-realtime` | `src/RealtimeService/Dockerfile` |
| `pollbuilder-gateway` | `src/ApiGateway/Dockerfile` |

Leave the Docker build context at the repository root — every image also compiles
`PollBuilder.Contracts`.

Then set the environment variables from the table above. Create the gateway last, once you know the
other three URLs.

### 3. GitHub secrets

`DOCKER_USERNAME`, `DOCKER_PASSWORD`, and one deploy hook per service:
`RENDER_DEPLOY_HOOK_GATEWAY`, `RENDER_DEPLOY_HOOK_POLL`, `RENDER_DEPLOY_HOOK_VOTING`,
`RENDER_DEPLOY_HOOK_REALTIME`.

### 4. Frontend

Point the SPA at the gateway with a single environment variable in Vercel:

```
VITE_API_BASE_URL=https://pollbuilder-gateway.onrender.com
```

SignalR connects to `${VITE_API_BASE_URL}/hubs/poll`, which the gateway proxies to RealtimeService.
If Render's free tier ever misbehaves on the WebSocket upgrade, set `VITE_REALTIME_URL` to the
RealtimeService URL to bypass the gateway for the socket only.

### Before the demo

Render's free tier spins a service down after 15 minutes idle and takes roughly 50 seconds to wake.
**Open all four `/health` URLs a couple of minutes before presenting.**

---

## CI/CD

| Workflow | Trigger | What it does |
|---|---|---|
| `.github/workflows/ci.yml` | PR into `develop`, pushes to `develop`/`feature` | `dotnet format --verify-no-changes` (lint / static analysis) → build → unit + integration tests → builds all four Docker images without pushing. Never deploys. |
| `.github/workflows/cd.yml` | push to `main` | Builds and pushes four images to Docker Hub tagged with the commit SHA and `latest`, then triggers the four Render deploy hooks. |

Branching follows the workflow taught in class: `feature` → `develop` → `main`.

---

## Repository layout

```
├── src/
│   ├── ApiGateway/             Ocelot gateway; ocelot.json holds the public route table
│   ├── PollBuilder.Contracts/  DTOs, errors and startup helpers shared by all services
│   ├── PollService/            Polls and options
│   ├── VotingService/          Votes and results
│   └── RealtimeService/        SignalR hub
├── tests/
│   ├── PollService.Tests/      unit + integration
│   ├── VotingService.Tests/    unit + integration
│   └── RealtimeService.Tests/  SignalR end-to-end
├── docs/API_CONTRACT.md        the contract the SPA is built against
├── docker-compose.yml          the whole stack locally
└── Directory.Build.props       shared build settings and analyzer configuration
```

Within each service the layout follows the convention from the microservices lab:
`Models/` → `Data/` → `Repo/` (`IXRepo` + `XRepo`) → `Services/` → `Controllers/`.
