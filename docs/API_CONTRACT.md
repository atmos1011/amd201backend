# API Contract — Poll & Survey Builder

Everything the Vue SPA needs. **Call the gateway only.** One base URL, one CORS origin:

```
VITE_API_BASE_URL = https://<gateway>.onrender.com     # http://localhost:5000 locally
```

The three services behind the gateway are an implementation detail; their URLs are not part of this
contract and may change.

Interactive docs (Scalar) per service: `/docs` — useful for poking at requests by hand.

---

## The two tokens

| Token | Who holds it | Sent as | Lives in |
|---|---|---|---|
| `creatorToken` | whoever created the poll | `X-Creator-Token` header | `localStorage` under `poll:<code>:creator` |
| `voterToken` | every respondent's browser | `X-Voter-Token` header | `localStorage` under `voterToken` (one for all polls) |

**`creatorToken` is returned exactly once**, in the `POST /api/polls` response. It is stored hashed
on the server and can never be read back. If it is lost, the poll can never be edited or closed —
so persist it immediately after creating a poll.

**`voterToken`** does not need to exist before the first vote. Send the vote without the header and
the server issues one, returned in both the response body (`voterToken`) and the `X-Voter-Token`
response header. Store it and send it on every later request.

---

## Endpoints

### `POST /api/polls` — create a poll

```jsonc
// request
{
  "question": "Tabs or spaces?",     // 3-300 characters
  "options":  ["Tabs", "Spaces"],    // 2-6 items, non-blank, unique, max 200 chars each
  "expiresAt": "2026-09-01T10:00:00Z" // optional, must be in the future
}
```

```jsonc
// 201 Created
{
  "code": "7fGh2a",
  "question": "Tabs or spaces?",
  "status": "Open",
  "createdAt": "2026-08-18T09:44:15.9351786+00:00",
  "expiresAt": null,
  "options": [
    { "index": 0, "text": "Tabs" },
    { "index": 1, "text": "Spaces" }
  ],
  "creatorToken": "pm2g-CWZ4fiFMdj3Hsths5LEPz5ERT-x",  // save this now, shown once
  "shareUrl": "https://myapp.vercel.app/poll/7fGh2a"
}
```

`400` if validation fails, as ProblemDetails with a per-field `errors` object.

---

### `GET /api/polls/{code}` — read a poll

```jsonc
// 200 OK
{
  "code": "7fGh2a",
  "question": "Tabs or spaces?",
  "status": "Open",              // "Open" | "Closed"
  "createdAt": "2026-08-18T09:44:15.93+00:00",
  "expiresAt": null,
  "closedAt": null,
  "acceptsVotes": true,          // false when closed OR expired - use this, not status
  "hasVotes": false,             // true once anyone has voted; question/options are frozen
  "options": [
    { "index": 0, "text": "Tabs" },
    { "index": 1, "text": "Spaces" }
  ]
}
```

`404` if the code does not exist. No vote counts here — those come from the results endpoint.

> Render `acceptsVotes`, not `status`. A poll past its `expiresAt` reports `status: "Closed"` on the
> next read, but `acceptsVotes` is the single flag that decides whether the vote form is usable.

---

### `POST /api/polls/{code}/vote` — cast a vote

```
X-Voter-Token: <token>        optional on the first vote
```

```jsonc
// request
{ "optionIndex": 0 }
```

```jsonc
// 200 OK
{
  "voterToken": "3s9-...-xQ",   // save it if you did not have one
  "results": { /* the results payload below */ }
}
```

| Status | `errorCode` | What the UI should do |
|---|---|---|
| `400` | `invalid_option` | The option index does not exist on this poll — refresh the poll. |
| `404` | `poll_not_found` | Show "this poll no longer exists". |
| `409` | `already_voted` | Switch to the results view; this browser has voted. |
| `409` | `poll_closed` | Show "voting has closed" and switch to results. |
| `429` | — | Rate limited (30 votes/minute per IP). Back off and retry. |
| `503` | `upstream_unavailable` | A service is waking up. Retry once after a few seconds. |

The response already contains fresh results, so there is no need to re-fetch after voting.

---

### `GET /api/polls/{code}/results` — current tallies

```jsonc
// 200 OK
{
  "code": "7fGh2a",
  "question": "Tabs or spaces?",
  "status": "Open",
  "acceptsVotes": true,
  "totalVotes": 3,
  "updatedAt": "2026-08-18T09:53:19.45+00:00",
  "options": [
    { "index": 0, "text": "Tabs",   "votes": 2, "percentage": 66.7 },
    { "index": 1, "text": "Spaces", "votes": 1, "percentage": 33.3 }
  ]
}
```

Every option is always present, including ones with zero votes, so the chart never changes shape as
votes arrive. `percentage` is rounded to one decimal and is `0` when `totalVotes` is `0`.

**This is the exact shape pushed over SignalR as `ResultsUpdated`** — write one render function and
use it for both.

---

### `GET /api/polls/{code}/vote/me` — has this browser voted?

```
X-Voter-Token: <token>
```

```jsonc
// 200 OK
{ "hasVoted": true }
```

Use it when a results link is opened directly, to decide whether to show the vote form.

---

### `PATCH /api/polls/{code}` — edit or close (creator only)

```
X-Creator-Token: <token>
```

Send only the fields being changed; anything omitted is left alone.

```jsonc
{
  "question": "New question?",           // optional
  "options": ["A", "B", "C"],            // optional, replaces all options
  "expiresAt": "2026-09-01T10:00:00Z",   // optional
  "clearExpiresAt": true,                // optional, removes an existing expiry
  "status": "Closed"                     // optional: "Closed" to close, "Open" to reopen
}
```

Returns the updated poll (same shape as `GET`).

| Status | `errorCode` | Meaning |
|---|---|---|
| `400` | — | Empty body, or `expiresAt` together with `clearExpiresAt`. |
| `403` | `not_creator` | Missing or wrong `X-Creator-Token`. |
| `409` | `poll_has_votes` | Question/options cannot change once voting has started. Closing is still allowed. |

Reopening a poll whose expiry has already passed clears the expiry, so it does not immediately
close again.

---

### `PUT /api/polls/{code}` — replace a poll (creator only)

Same auth and error codes as `PATCH`, but every field is required and the poll ends up exactly as
described:

```jsonc
{
  "question": "Tabs or spaces?",
  "options": ["Tabs", "Spaces"],
  "expiresAt": null,
  "status": "Open"
}
```

Submitting the *unchanged* question and options is not treated as an edit, so a form that PUTs
everything back can still close a poll that already has votes.

---

### `POST /api/polls/{code}/close` — close (creator only)

Shortcut for `PATCH { "status": "Closed" }`. No body. Requires `X-Creator-Token`.

---

### `GET /api/polls/{code}/results.csv` — export

`text/csv`, `Content-Disposition: attachment`. Columns:
`option_index,option_text,votes,percentage`.

### `GET /api/polls/{code}/qr` — QR code

`image/png` of the share link. Use it directly as an `<img src>`; no auth needed.

---

## Realtime — SignalR

```js
import { HubConnectionBuilder, HttpTransportType } from '@microsoft/signalr'

const connection = new HubConnectionBuilder()
  .withUrl(`${import.meta.env.VITE_API_BASE_URL}/hubs/poll`)
  .withAutomaticReconnect()
  .build()

connection.on('ResultsUpdated', results => renderChart(results))   // same shape as GET results
connection.on('PollClosed', ({ code, status, closedAt }) => disableVoting())

await connection.start()
await connection.invoke('JoinPoll', code)      // when the results page opens
// ...
await connection.invoke('LeavePoll', code)     // when it closes or navigates away
await connection.stop()
```

| Direction | Name | Payload |
|---|---|---|
| client → server | `JoinPoll(code)` | subscribe to one poll's updates |
| client → server | `LeavePoll(code)` | unsubscribe |
| server → client | `ResultsUpdated` | the results object above |
| server → client | `PollClosed` | `{ code, status: "Closed", closedAt }` — **no tallies**; re-fetch results or just disable voting |

Notes:

- Fetch results once with `GET .../results` when the page loads, then let pushes update it. A push
  only fires when something changes, so a page opened mid-poll would otherwise be empty.
- `withAutomaticReconnect()` matters on Render's free tier — a sleeping instance drops sockets.
  Re-`invoke('JoinPoll', code)` in the `onreconnected` handler; group membership does not survive a
  reconnect.
- If the WebSocket upgrade ever fails through the gateway, point the hub at RealtimeService directly
  via `VITE_REALTIME_URL` — everything else keeps going through the gateway.

---

## Errors

Every error is RFC 7807 ProblemDetails with an extra `errorCode` field:

```jsonc
{
  "type": "https://tools.ietf.org/html/rfc9110#section-15.5.10",
  "title": "Conflict",
  "status": 409,
  "detail": "This respondent has already voted in poll '7fGh2a'.",
  "errorCode": "already_voted"
}
```

Branch on `errorCode`, never on `detail` — the wording may change, the codes will not.

Full list: `poll_not_found`, `poll_closed`, `already_voted`, `invalid_option`, `not_creator`,
`poll_has_votes`, `code_generation_failed`, `upstream_unavailable`.

Validation failures (`400`) come from ASP.NET model validation and carry an `errors` object keyed by
field name instead of an `errorCode`.

---

## Suggested SPA flow

1. **Create page** — `POST /api/polls`, store `creatorToken`, show `shareUrl` and the QR image.
2. **Vote page** (`/poll/:code`) — `GET /api/polls/{code}`; if `acceptsVotes` is false, or
   `GET .../vote/me` says `hasVoted`, go straight to results. Otherwise show the form and
   `POST .../vote`.
3. **Results page** (`/poll/:code/results`) — `GET .../results` once, connect SignalR, `JoinPoll`,
   animate the bars on each `ResultsUpdated`. If the viewer holds the `creatorToken`, show a Close
   button wired to `POST .../close`.
