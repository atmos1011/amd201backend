# API Contract — Poll & Survey Builder

Everything the Vue app needs. **Only call the gateway.** One base URL:

```
VITE_API_BASE_URL = https://pollbuilder-gateway-rj0d.onrender.com/api
                    http://localhost:5000/api            # backend running locally
```

There are three services behind the gateway, but the frontend never needs to know that.

---

## The two tokens

| Token | Who has it | Sent as | Where to keep it |
|---|---|---|---|
| `creatorToken` | the person who made the poll | `X-Creator-Token` header | `localStorage`, key `poll:<code>:creator` |
| `voterToken` | every voter's browser | `X-Voter-Token` header | `localStorage`, key `voterToken` |

**`creatorToken` comes back only once**, in the response to `POST /api/polls`. Save it straight
away — without it the poll can never be closed or edited.

**`voterToken`** does not need to exist yet. Send the first vote with no header and the server
creates one, returning it in the response body and in the `X-Voter-Token` response header. Save it
and send it on every request after that.

---

## Endpoints

### `POST /api/polls` — create a poll

```jsonc
// request
{
  "question": "Tabs or spaces?",   // 3 to 300 characters
  "options": ["Tabs", "Spaces"]    // 2 to 6 options
}
```

```jsonc
// 201 Created
{
  "creatorToken": "3cda6c0bde01430390e6706bdf2cccc8",  // save this, shown once
  "shareUrl": "http://localhost:5173/poll/BMSfWr",
  "code": "BMSfWr",
  "question": "Tabs or spaces?",
  "status": "Open",
  "createdAt": "2026-08-19T16:46:11.78Z",
  "closedAt": null,
  "hasVotes": false,
  "options": [
    { "index": 0, "text": "Tabs" },
    { "index": 1, "text": "Spaces" }
  ]
}
```

`400` if the question or options fail validation.

---

### `GET /api/polls/{code}` — read a poll

Returns the same object without `creatorToken` and `shareUrl`. `404` if the code is unknown.

- `status` is `"Open"` or `"Closed"` — show the vote form only when it is `"Open"`.
- `hasVotes` is `true` once anyone has voted; the question and options are frozen from then on.

---

### `POST /api/polls/{code}/vote` — vote

```
X-Voter-Token: <token>      not needed on the very first vote
```

```jsonc
// request
{ "optionIndex": 0 }
```

```jsonc
// 200 OK
{
  "voterToken": "8f2c...",         // save it if you did not have one
  "results": { /* the results object below */ }
}
```

`results` can be `null` in the rare case the results service could not be reached. The vote is still
saved, so treat `null` as "fetch the results yourself" rather than as a failure.

| Status | `error` | Meaning | What the UI should do |
|---|---|---|---|
| `400` | `invalid_option` | that option is not on the poll | refresh the poll |
| `404` | — | no poll with that code | show "this poll does not exist" |
| `409` | `already_voted` | this browser has voted already | switch to the results view |
| `409` | `poll_closed` | the poll stopped accepting votes | show the results, say it closed |

**Two different things return `409`**, so branch on `error`, not on the status code. Every failure
response has the same shape:

```jsonc
{ "error": "already_voted", "message": "You have already voted in this poll." }
```

`error` is stable and safe to compare against. `message` is written for a person and may change.

The response already contains the new results, so there is no need to fetch them again.

---

### `GET /api/polls/{code}/results` — live results

```jsonc
// 200 OK
{
  "code": "BMSfWr",
  "question": "Tabs or spaces?",
  "status": "Open",
  "totalVotes": 2,
  "options": [
    { "index": 0, "text": "Tabs",   "votes": 1, "percentage": 50 },
    { "index": 1, "text": "Spaces", "votes": 1, "percentage": 50 }
  ]
}
```

Every option is always there, even with zero votes, so the chart keeps the same shape as votes come
in. `percentage` is rounded to one decimal, and is `0` when nobody has voted yet.

**This is exactly what SignalR sends**, so write one function that draws the chart and use it for
both the first fetch and every live update.

---

### `GET /api/polls/{code}/vote/me` — has this browser voted?

```
X-Voter-Token: <token>
```

```jsonc
{ "hasVoted": true }
```

Useful when someone opens a poll link directly and you need to decide whether to show the form or
the results.

---

### `GET /api/polls/{code}/results.csv` — download the results

Returns a CSV file, for opening in Excel. Handy for the demo.

---

### `PATCH /api/polls/{code}` — edit or close (creator only)

```
X-Creator-Token: <token>
```

Send only what is changing:

```jsonc
{
  "question": "New question?",     // optional
  "options": ["A", "B", "C"],      // optional, replaces all options
  "status": "Closed"               // optional: "Closed" to close, "Open" to reopen
}
```

Returns the updated poll.

| Status | Meaning |
|---|---|
| `403` | missing or wrong `X-Creator-Token` |
| `404` | no poll with that code |
| `409` | the poll already has votes, so the question and options cannot change (closing still works) |

### `PUT /api/polls/{code}` — replace a poll (creator only)

Same rules, but `question` and `options` are both required and replace what is there.

### `POST /api/polls/{code}/close` — close the poll (creator only)

No body. The same as `PATCH { "status": "Closed" }`.

---

## Live updates with SignalR

```js
import { HubConnectionBuilder } from '@microsoft/signalr'

const connection = new HubConnectionBuilder()
  .withUrl(`${import.meta.env.VITE_API_BASE_URL}/hubs/poll`)
  .withAutomaticReconnect()
  .build()

// same object as GET /api/polls/{code}/results
connection.on('ResultsUpdated', results => drawChart(results))

await connection.start()
await connection.invoke('JoinPoll', code)     // when the results page opens
// ...
await connection.invoke('LeavePoll', code)    // when leaving the page
await connection.stop()
```

| Direction | Name | Payload |
|---|---|---|
| client → server | `JoinPoll(code)` | start receiving updates for this poll |
| client → server | `LeavePoll(code)` | stop receiving them |
| server → client | `ResultsUpdated` | the results object |

Things to watch out for:

- Fetch the results once with `GET .../results` when the page loads. Updates are only sent when
  somebody votes, so a page opened halfway through a poll would otherwise show nothing.
- Keep `withAutomaticReconnect()`. Render's free tier drops connections when a service sleeps.
  After a reconnect the group membership is gone, so call `JoinPoll` again in the `onreconnected`
  handler.
- Closing a poll is **not** pushed over SignalR. The results page finds out the next time it calls
  `GET .../results`, where `status` will be `"Closed"`.

---

## Suggested pages

1. **Create page** — `POST /api/polls`, save `creatorToken`, show `shareUrl`.
2. **Vote page** (`/poll/:code`) — `GET /api/polls/{code}`. If `status` is `"Closed"`, or
   `GET .../vote/me` says `hasVoted`, go to the results instead. Otherwise show the form and
   `POST .../vote`.
3. **Results page** (`/poll/:code/results`) — `GET .../results` once, connect SignalR, `JoinPoll`,
   and animate the bars on every `ResultsUpdated`. If this browser saved the `creatorToken`, show a
   Close button that calls `POST .../close`.
