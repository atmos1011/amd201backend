# Poll & Survey Builder — Backend

Backend for the AMD201 45H Workshop assignment. A poll builder where anyone can create a
multiple-choice question, share a short link, collect votes, and watch the results update live
without refreshing the page.

It is built as **three ASP.NET Core services**, following the microservices pattern from class:
an Ocelot API gateway in front, and two Web API services behind it, both using PostgreSQL on Neon.
The Vue SPA is deployed separately on Vercel and only ever calls the gateway.

- **Live gateway:** _fill in after deploying_
- **Live frontend:** _fill in after deploying_
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
                      |  HTTP + WebSocket
                      v
        +---------------------------+
        |   ApiGateway (Ocelot)     |   <- the only address the SPA knows
        +----+-----------------+----+
             |                 |
             v                 v
    +----------------+  +------------------------+
    |  PollManage    |  |      VoteManage        |
    |  Polls,        |  |  Votes + SignalR hub   |
    |  PollOptions   |<-|  (asks PollManage      |
    +--------+-------+  |   about the poll)      |
             |          +-----------+------------+
             |                      |
             v                      v
        +-------------------------------+
        |   Neon PostgreSQL: pollbuilder |
        +-------------------------------+
```

| Project | What it owns | What it does |
|---|---|---|
| **ApiGateway** | nothing | Ocelot. Turns one public URL into calls to the two services, and passes the SignalR WebSocket through. |
| **PollManage** | `Polls`, `PollOptions` | Create a poll, read it, edit it (`PUT`/`PATCH`), close it. |
| **VoteManage** | `Votes` | Record a vote, work out the results, and push them live over SignalR. |

### How VoteManage talks to PollManage

VoteManage has no `Polls` table, so before saving a vote it asks PollManage for the poll — through
the gateway, exactly like `StudentService` does in the microservices lab:

```csharp
var requestPath = $"api/polls/{code}";           // path on the gateway
var response = await _httpClient.GetAsync(requestPath);
```

It checks the poll exists, that its status is `Open`, and that the chosen option is really on the
poll. After the first vote it calls `api/polls/{code}/votes-recorded` so PollManage can set
`HasVotes = true` and stop the creator editing the question underneath people who already voted.

### Decisions worth explaining in the presentation

| Decision | Why |
|---|---|
| Votes store the poll **code**, not a foreign key | The `Polls` table belongs to PollManage. A foreign key across services would tie them together and undo the point of splitting them. |
| One database for both services | Same as the lab, where all three services share one Neon database. Simple, and free. |
| One vote per browser is a **unique index** on `(PollCode, VoterToken)` | The C# check runs first, but two fast clicks can both pass it. The database index is what actually stops the second vote. |
| Voter token in `localStorage`, not a cookie | Vercel and Render are different sites, and browsers such as Safari block cross-site cookies. That would break the demo. |
| The creator gets a token, and it is needed to edit or close | Otherwise any voter could close someone else's poll. |
| Editing is blocked once voting starts | Changing an option after votes exist would move those votes onto different text. |
| `StatusCode(403)` instead of `Forbid()` | `Forbid()` needs an authentication scheme configured, and this project has none, so it throws a 500. |

---

## Running it locally

Set the connection string first — see [Configuration](#configuration).

In Visual Studio: right-click the solution → **Properties** → **Configure Startup Projects** →
**Multiple startup projects**, and start **PollManage**, **VoteManage** and **ApiGateway**
(the gateway last, same as the lab).

From a terminal:

```bash
dotnet run --project PollManage      # http://localhost:5101
dotnet run --project VoteManage      # http://localhost:5102
dotnet run --project ApiGateway      # http://localhost:5000
```

Then call the gateway — never the services directly:

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

PollManage and VoteManage each have Swagger at `/swagger` for trying requests by hand.

### Tests

```bash
dotnet test
```

Thirteen tests covering the repositories: saving and finding polls, locking a poll once it has
votes, one vote per browser, and the vote counting behind the results chart.

### Formatting

```bash
dotnet format --verify-no-changes
```

This is the check the CI pipeline runs. Run `dotnet format` with no arguments to fix problems.

### Migrations

The tables are created from EF Core migrations. In Visual Studio's Package Manager Console, with
the right project selected:

```
add-migration addpoll
update-database
```

Or from a terminal:

```bash
dotnet ef migrations add addpoll --project PollManage
dotnet ef migrations add addvote --project VoteManage
```

The migrations are already committed, and both services run `Database.Migrate()` on startup, so
the tables appear by themselves the first time the app runs against an empty database. Render has
no separate step for running migrations, which is why it is done in `Program.cs`.

---

## Configuration

| Setting | Where | What it is |
|---|---|---|
| `ConnectionStrings:myContext` | PollManage, VoteManage | The Neon connection string. |
| `ServiceEndpoints:ApiGatewayBaseUrl` | VoteManage | The gateway URL, so VoteManage can ask PollManage about a poll. |
| `ShareBaseUrl` | PollManage | The Vue app's URL, used to build the share link. |

For local work these live in `appsettings.Development.json`, which is **gitignored** because it
holds the real Neon password.

In production every one of them is an environment variable on Render instead, using `__` between
the levels:

```
ConnectionStrings__myContext=Host=ep-....neon.tech;Port=5432;Database=pollbuilder;Username=...;Password=...;SSL Mode=Require
ServiceEndpoints__ApiGatewayBaseUrl=https://pollbuilder-gateway.onrender.com
ShareBaseUrl=https://your-app.vercel.app
```

> Get the connection string from the Neon dashboard: **Connection string** panel → format
> **.NET** → keep **Pooled connection** ticked. Pick a Neon region near your Render region.

---

## Deployment

### 1. Neon

Create a project and a database called `pollbuilder`. Both services share it, the same way all
three services in the lab share one database.

### 2. Render — three web services

For each: **New → Web Service**, point it at this repository, choose **Docker**, and set the
**Root Directory** to the project folder so the Dockerfile inside it is used:

| Render service | Root Directory |
|---|---|
| `pollbuilder-pollmanage` | `PollManage` |
| `pollbuilder-votemanage` | `VoteManage` |
| `pollbuilder-gateway` | `ApiGateway` |

Add the environment variables above. Create the gateway last, once the other two URLs exist.

### 3. Point the gateway at the deployed services

`ApiGateway/ocelot.json` holds the routes for local development, with `localhost` addresses.
`ApiGateway/ocelot.Production.json` is the same list with the Render addresses, and Render loads it
automatically because it sets `ASPNETCORE_ENVIRONMENT=Production`.

**Edit `ocelot.Production.json` and replace the two host names with your own Render URLs**, then
commit. That is the only file that needs to change after deploying.

### 4. GitHub secrets

`DOCKER_USERNAME`, `DOCKER_PASSWORD`, `RENDER_DEPLOY_HOOK_POLL`, `RENDER_DEPLOY_HOOK_VOTE`,
`RENDER_DEPLOY_HOOK_GATEWAY`.

### 5. Frontend

One environment variable in Vercel:

```
VITE_API_BASE_URL=https://pollbuilder-gateway.onrender.com
```

### Before the demo

Render's free tier puts a service to sleep after 15 minutes and takes about 50 seconds to wake it
up. **Open all three services in a browser a couple of minutes before presenting.**

---

## CI/CD

| Workflow | Runs on | What it does |
|---|---|---|
| `.github/workflows/ci.yml` | pull request into `develop`, pushes to `develop` and `feature` | `dotnet format --verify-no-changes` (the linting step), then build, then test. It never deploys. |
| `.github/workflows/cd.yml` | push to `main` | Builds a Docker image for each of the three projects, pushes them to Docker Hub, and calls each Render deploy hook. |

Branches follow the order taught in class: `feature` → `develop` → `main`.

---

## Project layout

```
PollBuilder.slnx
ApiGateway/            Ocelot gateway
  ocelot.json              routes for local development
  ocelot.Production.json   the same routes, pointing at Render
PollManage/            polls and options
  Models/  Data/  Repo/  Controllers/  Migrations/
VoteManage/            votes, results and the SignalR hub
  Models/  Data/  Repo/  Services/  Hubs/  Controllers/  Migrations/
PollBuilder.Tests/     unit tests for both repositories
docs/API_CONTRACT.md   what the frontend needs to know
```

Each service follows the same layout as the microservices lab: `Models` → `Data/myContext.cs` →
`Repo` (`IXRepo` and `XRepo`) → `Services` for calling another service → `Controllers`.
