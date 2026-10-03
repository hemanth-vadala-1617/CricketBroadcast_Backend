# Cricket Broadcast Stream — API

Manual cricket scoring + TV-graphics backend. A scorer enters every ball; the server computes the whole match state and pushes it in real time to the OBS overlay, the producer console and the public page.

Video/audio is **not** handled here. Stream with OBS (overlay page as a Browser Source + your own mic/video) to YouTube/Facebook. Ball-by-ball text commentary was removed on purpose (it is spoken in OBS).

## Stack
.NET 9 · ASP.NET Core · EF Core 9 + PostgreSQL (snake_case) · ASP.NET Identity + JWT · SignalR · FluentValidation · Serilog

```
src/
  CricketStream.Domain          entities, enums, pure cricket rules (CricketRules) — no EF, no HTTP
  CricketStream.Contracts       request/response DTOs (MatchStateDto is the one realtime payload)
  CricketStream.Application     services, MatchStateBuilder (projection), BallEngine, validators, Result<T>
  CricketStream.Infrastructure  EF Core DbContext, migrations, Identity/JWT, seeder
  CricketStream.API             controllers, SignalR hub, Program.cs
tests/
  CricketStream.Domain.Tests        rule tests (extras, free hit, strike, targets, overs-left-today)
  CricketStream.Application.Tests   full match replays through the real services (in-memory EF)
```

## Run it
```powershell
# one-time: secrets live OUTSIDE the repo
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Host=localhost;Port=5432;Database=CricketBroadcastStream;Username=postgres;Password=<pw>" --project src/CricketStream.API
dotnet user-secrets set "JwtSettings:Secret" "<at least 32 random characters>" --project src/CricketStream.API
dotnet user-secrets set "Seed:AdminPassword" "<admin password>" --project src/CricketStream.API

dotnet run --project src/CricketStream.API        # http://localhost:5141 , Development: auto-migrates + seeds
dotnet test CricketStream.slnx
```
In Development the first start creates the schema, the roles, an admin user (`Seed:AdminEmail`, default `admin@cricketstream.com`), the rule presets (T20, T10, ODI, Test 5-day), a default overlay theme and, because `Seed:SampleData` is on, an India vs Australia sample (2 squads, 1 tournament, 1 venue). In Production set the same keys as environment variables (`ConnectionStrings__DefaultConnection`, `JwtSettings__Secret`, …), set `Database:AutoMigrate` deliberately, and leave `Seed:SampleData` off. The app refuses to start without a connection string or a JWT secret of 32+ characters.

## Design rules that matter
* **The ball log is the source of truth.** Totals, batter/bowler figures, partnerships, fall of wickets, timeline and the whole `MatchStateDto` are recomputed from the non-voided balls by `MatchStateBuilder`. Undo *voids* a ball and rewinds the crease to the exact pre-ball positions stored on that ball.
* **The server decides.** The scorer sends runs/extras/wicket only — never who is on strike or bowling. Free hit, wide/no-ball rules, bowler eligibility (no consecutive overs, max overs), dismissal validity and match results are all enforced in `Domain/Application`.
* **One realtime message**: `MatchStateUpdated(MatchStateDto)` with a monotonically increasing `version` (also the optimistic-concurrency token — a second scorer gets **409**, not corruption). The hub is read-only; every mutation is an authorised REST call. `JoinMatch(matchId)` replies with the current state, which doubles as the resync after a reconnect.
* **Idempotent scoring**: every ball carries a `clientRequestId` (unique index); a retried request returns the current state instead of a second ball.
* **No exceptions for expected failures**: services return `Result<T>`; controllers map them to 400/404/409/422 ProblemDetails.
* `DateTimeOffset` values are normalised to UTC (Npgsql only accepts offset 0).

## API at a glance
| Area | Endpoints |
|---|---|
| Auth | `POST /api/auth/login` · `/refresh` · `/logout` · `GET /api/auth/me` |
| Master data (admin writes) | `/api/teams` · `/api/players` · `/api/venues` · `/api/tournaments` · `/api/match-rules` · `/api/themes` · `POST /api/media` (image upload) |
| Match lifecycle | `/api/matches` (CRUD) · `PUT {id}/squad` · `POST {id}/toss` · `/start` · `/innings/start` · `/innings/end` · `/complete` · `/pause` · `/resume` · `/clock/advance` · `PUT /clock/overs-lost` · `PUT /win-probability` · `GET {id}/state` |
| Scoring | `POST /api/scoring/match/{id}/ball` · `/new-batter` · `/new-bowler` · `/swap-strike` · `/retire` · `/penalty` |
| Corrections | `POST /api/revisions/match/{id}/undo` · `PUT …/last-ball` (edit) · `POST …/cancel-innings` |
| Overlay | `PUT /api/graphics/match/{id}/{Scorebug\|BatterCards\|BowlerCard\|Timeline\|WinProbability\|FullScorecard\|Banner}` |
| Public (anonymous, rate-limited) | `GET /api/public/matches?status=live` · `GET /api/public/matches/{id}/state` |
| Realtime | SignalR `/hubs/match` — `JoinMatch(guid)` / `LeaveMatch(guid)`; server → client `MatchStateUpdated` |

Roles: `SuperAdmin`, `Admin` (everything) · `Scorer` (toss/start/score/undo/clock/graphics) · `Producer` (graphics, win probability) · `Viewer`.

## Known limits (deliberate)
* One match scored at a time is the target; nothing breaks with more, but there is no per-match staff assignment.
* Undo walks back one ball at a time. To correct an older ball, undo back to it and re-score (this keeps strike/over state consistent by construction).
* DLS / revised targets and super overs are not modelled: enter the result by hand with `/complete`. Win probability is manual.
* Uploaded images go to the local `uploads/` folder (override with `Uploads:Path`). Move to object storage if you deploy to several machines.
