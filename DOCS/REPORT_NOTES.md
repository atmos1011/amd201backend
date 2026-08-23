# Individual Report — working notes

**What this is:** the raw material for your report, organised into the template's sections, with
every fact checked against the code and the git history.

**What this is not:** the report itself. It's individually assessed and worth 30%, and a marker can
tell the difference between someone recounting their own work and prose that arrived from somewhere
else — the specifics of *your* experience are exactly what earns the marks. Everything below is
true and you were there for all of it, so writing it in your own words should be quick.

If your module requires you to declare AI assistance, check the policy and declare it. That's a
question for your lecturer, not for me.

---

## What the report has to cover

The template gives you the section headings; the brief gives you four things that must appear
somewhere inside them. Minimum **500 words** — aim for 650–750 so you're comfortably over.

| Template section | Brief requirement it satisfies | Words |
|---|---|---|
| Overview | — | ~60 |
| My Contribution | *What you personally built or contributed* | ~180 |
| Challenges | *A technical challenge you faced and how you resolved it* | ~250 |
| Learning outcomes | *What you learned from this project* | ~120 |
| Feedback on team members | *An honest peer assessment of each team member* | ~100 |
| Conclusion | — | ~40 |

**Header block:** Poll & Survey Builder · your name · student code · team · date.

**Repository links — put these in the Overview and make them clickable:**

- Backend — https://github.com/atmos1011/amd201backend
- Frontend — https://github.com/atmos1011/vuetest

---

## 1. Overview (~60 words)

One short paragraph. What the system is, what you owned, and the two links.

Facts you can use:

- A Poll & Survey Builder: create a multiple-choice poll, share a short link, collect votes, watch
  results update live without refreshing.
- Vue 3 SPA on Vercel; an Ocelot API gateway plus three ASP.NET Core services on Render;
  PostgreSQL on Neon.
- You owned the backend, database, CI/CD and deployment.

---

## 2. My Contribution (~180 words)

The most important section. Be concrete — name things, give numbers. Vague claims ("I worked on
the backend") score badly; specifics are unarguable.

**What you built**

- Four .NET projects: `ApiGateway` (Ocelot, 12 routes), `PollManage`, `VoteManage`, `ResultManage`
  — about 1,600 lines of C# excluding generated migrations.
- Database design and EF Core migrations: `Polls`, `PollOptions`, `Votes` on Neon PostgreSQL.
- The REST API the brief specifies, plus `PUT`/`PATCH` for editing and closing.
- SignalR hub for live results, with a group per poll so a vote only reaches people watching that
  poll.
- 13 unit tests, run by CI on every push.
- A Dockerfile per service, two GitHub Actions workflows, four Render services.
- The API contract document Phương built the frontend against.
- Later, when the frontend needed to be integration-ready for the demo, you wired it to the live
  API — the mapping layer in `pollService.js` and `signalr.js`, the creator-token storage, and the
  Close button.

**Architecture decisions worth naming** (these show judgement, not just effort)

- Votes reference a poll by **code, not a foreign key**, because the `Polls` table belongs to
  another service and a cross-service FK would defeat the split.
- One-vote-per-respondent is a **unique database index**, not just a C# check — because two
  simultaneous requests can both pass a check-then-insert.
- `ResultManage` owns **no tables at all**: it joins the other two services' data.

---

## 3. Challenges (~250 words)

Split it: one technical, one teamwork. The technical one is the brief's requirement, so give it
more room.

### 3.1 Technical — pick ONE and tell it as a diagnosis

The strongest is the **service discovery failure after deployment**, because the story has a real
shape: symptom → wrong hypothesis → method → cause → fix.

- Everything worked locally. In production, every vote returned `404` — as if no poll existed.
- Tempting wrong conclusion: the database, or CORS.
- Method: test each hop in isolation. PollManage directly → `201`. The gateway → `200`. The
  service-to-service call → `404`.
- Cause: Render appends a suffix when a service name is taken. We had planned
  `pollbuilder-gateway`; Render gave us `pollbuilder-gateway-rj0d`. `VoteManage` and `ResultManage`
  were still configured with the URL that never existed, so every outward call failed — and they
  correctly reported "poll not found".
- Fix: two environment variables. No code change.
- What it taught: a service that can't reach its dependency should fail loudly, not translate the
  failure into a normal-looking `404`.

**Alternatives if you'd rather use a different one:**

- The container started locally but aborted on Render with
  `inotify instance limit reached`. ASP.NET Core watches `appsettings.json` for changes, which uses
  inotify, and Render's free tier caps it. Fixed with `DOTNET_USE_POLLING_FILE_WATCHER=true` in the
  Dockerfile.
- The gateway routed `/api/polls/{code}/results` to the wrong service, because Ocelot's trailing
  `{code}` placeholder matches greedily and swallowed the longer path. Fixed with explicit route
  `Priority`, catch-all last.
- The pipeline reported deploys it never performed: the deploy step looked its secret up with
  `secrets[matrix.hook]`, which silently produced an empty string, so every job skipped while
  showing green.

### 3.2 Teamwork

Phương's version is already written, so make yours complementary rather than a restatement — same
events, your side of them.

Honest material:

- The API contract was still moving while the UI was being built in parallel, so we wrote it down
  (`DOCS/API_CONTRACT.md`) rather than agreeing it verbally.
- Two repositories with separate pipelines meant a change to a deployed URL had to land in both
  places, and neither of us could see the other's environment variables.
- Late in the project you took the frontend on to get it integrated for the demo.

> ⚠️ **Check this with Phương before you both submit.** Her 3.2 says *"by having me build a
> translation layer"*. The mapping layer in `pollService.js` was added later, during integration,
> when you took the frontend on. If both reports claim the same piece of work, a marker reading them
> side by side will notice. Easiest fix: agree who describes what, and each of you describe only
> what you did.

---

## 4. Learning outcomes (~120 words)

Avoid the generic list ("I learned teamwork and time management"). Tie each one to a moment.

- **Configuration is as much a part of deployment as code.** Three of the four production failures
  were environment variables and platform limits, not logic. The code was identical in both places.
- **Constraints belong in the database.** Application checks lose races. Five simultaneous votes
  from one token gave one `200` and four `409`s — the index is what did that, not the C#.
- **An error should say what went wrong.** Returning `409` for both "already voted" and "poll
  closed" meant the UI couldn't show the right message; adding a machine-readable code fixed it.
- **Free-tier platforms have real limits** — cold starts, inotify caps, sleeping services — and you
  design around them or your demo fails.

---

## 5. Feedback on team members and the assignment (~100 words)

The brief asks for an **honest** peer assessment. Neither flattery nor a complaint list — be
specific and fair, and note what someone did well before what didn't work.

Fair things to say about Phương's work (all verifiable in the frontend repo):

- Built a complete, well-organised Vue 3 app: a design-token stylesheet, reusable components, and
  clean separation between views and services.
- Put the API calls behind a service layer with comments saying *"adjust here if the real API
  differs"* — that seam is precisely why adapting the frontend to the real backend needed almost no
  changes to her components. That was good design, and worth crediting.
- Be factual about the handover: describe what happened, not a verdict on her.

On the assignment itself, one or two honest lines — for example that the microservices requirement
added a lot of deployment complexity relative to the size of the app, and that the free-tier hosting
constraints were a bigger part of the work than expected.

---

## 6. Conclusion (~40 words)

What works now, in one or two sentences. The system is deployed and verified end to end: create,
vote, duplicate rejected, live push, close. 13 unit tests plus 23 API assertions passing against
production.

---

## Numbers you can quote

| | |
|---|---|
| Backend commits | 19, from 17 to 21 August |
| Frontend commits | 7 |
| C# excluding migrations | ~1,600 lines across four projects |
| Gateway routes | 12 |
| Unit tests | 13 |
| API assertions (Postman) | 23, green against production |
| Services deployed | 4 on Render, 1 on Vercel, 1 Neon database |

---

## Before you submit

- Word count over 500.
- Both repository links present and clickable.
- All four brief requirements covered: what you built, a technical challenge, what you learned,
  peer assessment.
- Nothing claimed that a marker could check and find untrue — especially around the tests, which
  are unit tests plus a manual Postman suite, not automated integration tests.
