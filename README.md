# Poll & Survey Builder — Backend

AMD201 45H Workshop. An Ocelot API gateway plus three ASP.NET Core services, backed by
PostgreSQL on Neon, deployed to Render. The Vue frontend lives in a separate repository.

**The documentation is in [`DOCS/`](DOCS/).**

| Document | What it is |
|---|---|
| [`DOCS/README.md`](DOCS/README.md) | Architecture, how to run it, configuration, deployment, CI/CD |
| [`DOCS/API_CONTRACT.md`](DOCS/API_CONTRACT.md) | Every endpoint, both token headers, and the SignalR contract |
| [`DOCS/PollBuilder.postman_collection.json`](DOCS/PollBuilder.postman_collection.json) | 17 requests, 23 assertions — import into Postman |
| [`DOCS/PollBuilder.http`](DOCS/PollBuilder.http) | The same requests, clickable in Visual Studio |
| [`DOCS/PRESENTATION_QA.html`](DOCS/PRESENTATION_QA.html) | Presentation question prep |
| [`DOCS/REPORT_NOTES.md`](DOCS/REPORT_NOTES.md) | Material for the individual report |

## Quick start

```bash
dotnet run --project PollManage      # http://localhost:5101
dotnet run --project VoteManage      # http://localhost:5102
dotnet run --project ResultManage    # http://localhost:5103
dotnet run --project ApiGateway      # http://localhost:5000  <- call this one
```

```bash
dotnet test        # 13 unit tests
```

Set `ConnectionStrings__myContext` first — see [`DOCS/README.md`](DOCS/README.md#configuration).
