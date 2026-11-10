# BubbleWorld Backend — Architecture

Architecture reference for the BubbleWorld game backend
(`D:\Unity Projects\BubbleWorld_Backend`), plus the recommended AWS deployment
target. Companion document: [`API.md`](./API.md) (endpoint-level reference).

---

## Table of contents

1. [Overview](#1-overview)
2. [System context](#2-system-context)
3. [High-level architecture](#3-high-level-architecture)
4. [Source layout & request pipeline](#4-source-layout--request-pipeline)
5. [Data architecture](#5-data-architecture)
6. [Auth & authorization](#6-auth--authorization)
7. [Core flows](#7-core-flows)
8. [Consistency & authority rules](#8-consistency--authority-rules)
9. [Reliability](#9-reliability)
10. [Security posture](#10-security-posture)
11. [Configuration & operations](#11-configuration--operations)
12. [Current limitations](#12-current-limitations)
13. [AWS deployment recommendation](#13-aws-deployment-recommendation)
14. [Evolution roadmap](#14-evolution-roadmap)

---

## 1. Overview

The backend is the **canonical authority for all Bubble World game state**.
The Unity client's local save and Nintendo Cloud Save are caches only — the
server owns persistence, versioning, simulation bookkeeping, economy (AP), the
Soulform brain (LLM), and pairing.

Design principles:

| Principle | How it shows up in code |
|---|---|
| **Server authoritative** | Every write goes through Zod validation + service logic; server recomputes version, season, weather, AP cost; conflicts resolve server-side (`world_state_snapshots` version bumps) |
| **Per-player authorization on every request** | `requireAuth` + `requireWorldAccess(viewer\|member\|owner)` or shared-key check — no route is exempt |
| **Stateless HTTP, small process state** | JWT auth (no server session store); the only in-process state is the world-sync snapshot cache, preloaded at boot |
| **Two trust tiers** | JWT for account-facing routes; `x-api-key` shared secret for the login-less Unity client (same trust model as `/sync`) |
| **Fail-open where gameplay must continue** | Brain falls back to canned mock responses; preload failure falls back to DB reads; pairing cancel is best-effort |

**Stack:** Node.js + TypeScript, Express 5, PostgreSQL 14, Zod v4,
jsonwebtoken, `express-rate-limit`, hand-rolled SigV4 → AWS Bedrock.
Redis (`ioredis`/`redis`) is **installed but not yet wired** — see
[§12](#12-current-limitations) and [§13.5](#135-redis-elasticache--what-to-wire).

---

## 2. System context

```
                        ┌─────────────────────────────┐
                        │  Unity client (Switch / PC) │  no login — x-api-key
                        │  Brain, Sync, Pairing (QR)  │
                        └──────────────┬──────────────┘
                                       │ HTTPS JSON
┌──────────────────────────┐           ▼
│ Companion / mobile app   │   ┌───────────────────────────┐
│ JWT login, claim pairing │──▶│  BubbleWorld Backend      │
│ worlds, chat, live view  │   │  Express 5 · /api/v1      │
└──────────────────────────┘   └──────────┬────────────────┘
┌──────────────────────────┐              │ SigV4 (hand-rolled)
│ Ops / monitoring         │── /health    ▼
│ scripts, smoke tests     │      Amazon Bedrock Runtime
└──────────────────────────┘      (Llama 3.1 8B Instruct)
                 ▲                        ▲
                 │ SQL                    │ credentials
        ┌────────┴─────────┐              │
        │ PostgreSQL 14    │      AWS keys (today: static in .env;
        │ (canonical save) │      target: IAM task role — §13.6)
        └──────────────────┘
```

Clients:

- **Unity game client** — no login. Calls the shared-key routes
  (`/ai/chat`, `/sync/state`, `/pairing/device/session`) and renders the QR
  pairing overlay. Fails over local ↔ remote backend link and to mock brain
  responses when the backend is down.
- **Companion app (JWT)** — full account API: claims pairing codes, reads
  linked worlds, chats, views live state.
- **Test/ops scripts** — `smoke-test.ps1` (14 checks), `test-device-pairing.ps1`
  (11 checks) run against a live local server.

---

## 3. High-level architecture

```
                               HTTP (Express 5)
   ┌────────────────────────────────────────────────────────────────────┐
   │  index.ts  · helmet → cors → compression → json(50mb) → morgan     │
   │            → trust proxy → /api/v1 rate limiter                   │
   ├────────────────────────────────────────────────────────────────────┤
   │  Routes (9 files, all under /api/v1 + GET /health)                 │
   │   auth · players · worlds · entities · pairing · companion ·       │
   │   game-data · ai · sync                                            │
   ├────────────────────────────────────────────────────────────────────┤
   │  Middleware                │  Services                              │
   │   validation (Zod)         │   auth      player      world         │
   │   auth (JWT)               │   entity    simulation  companion     │
   │   sharedKey (x-api-key)    │   bedrock   worldSync   worldContext  │
   │   worldAccess (roles)      │   pairing                                 │
   │   rateLimiter (5 limiters) │                                          │
   │   errorHandler (AppError)  │                                          │
   ├───────────────────────────┴──────────────────────────────────────┤
   │  Persistence                                                        │
   │   pg Pool (2–10) · migrations embedded in migrate.ts (24 steps)     │
   │   worldSyncService in-memory Map (preloaded at boot, DB fallback)   │
   └─────────────────────────────────────────────────────────────────────┘
                                     │ SigV4 (bedrockService)
                                     ▼
                          AWS Bedrock Runtime · llama3-1-8b
                          3 gates + retries + mock fallback
```

Boot sequence (`src/index.ts:start()`):

1. `runMigrations()` — idempotent, tracked in `_migrations` (boot performs
   schema migration; a failure exits the process).
2. `preloadWorldSync()` — loads all `client_world_states` rows into the
   in-memory Map. **Non-fatal**: on error the routes fall back to DB reads.
3. `app.listen(port)`.

---

## 4. Source layout & request pipeline

```
src/
├── index.ts            app wiring, mounts, boot sequence
├── config.ts           single config object from .env (typed, defaults)
├── db/
│   ├── index.ts        pg Pool wrapper: db.query / db.getClient
│   └── migrate.ts      all 24 migrations as {name, sql} + runner
├── middleware/
│   ├── validation.ts   validate(schema) → Zod safeParse → AppError(400)
│   ├── auth.ts         requireAuth → JWT → req.accountId/deviceId
│   ├── sharedKey.ts    requireSharedKey → timing-safe x-api-key
│   ├── worldAccess.ts  requireWorldAccess(minRole) → DB role check
│   ├── rateLimiter.ts  api/auth/pairing/chat/action limiters
│   └── errorHandler.ts AppError + asyncHandler + notFound
├── routes/             thin: parse → authorize → call service → envelope
├── services/           all business logic + SQL
└── types/index.ts      shared DTO interfaces (WorldStateData, EntityData…)
```

**Pipeline per request:**

```
helmet → cors → compression → json(50MB) → morgan → apiLimiter
  → [router] → rateLimiter? → auth/sharedKey? → validate(Zod)?
  → worldAccess? → service (SQL, business rules) → {status,data} | AppError
```

Route handlers stay thin — they parse params with the stateless Express 5
helpers `param(req,'name')` / `queryParam(req,'name')` (never `req.params.x`
directly), call exactly one service function, and shape the envelope. All
business rules and SQL live in `services/`.

---

## 5. Data architecture

### 5.1 Schema (24 migrations, embedded in `migrate.ts`)

| Domain | Tables |
|---|---|
| Identity | `accounts`, `platform_identities`, `player_profiles`, `devices`, `sessions` |
| Worlds | `worlds`, `world_members` |
| Save versions | `world_state_snapshots` (gzip), `world_state_diffs` (gzip), `client_world_states` (opaque client blob) |
| Gameplay audit | `action_journal_entries`, `world_event_log`, `sync_checkpoints` |
| Simulation | `soulform_states`, `weather_history`, `world_cooldowns` |
| Entities | `entities` + `npc_states` / `animal_states` / `soulform_entity_states` (1:1) |
| World building | `bubbles`, `bubble_connections`, `tile_cells`, `tile_compositions`, `tile_states` |
| Economy | `ap_balances`, `architect_point_ledger` |
| Social | `chat_sessions`, `chat_messages`, `memory_chapters`, `memory_entries` |
| Pairing | `pairing_sessions` (nullable account/world + `world_key` after migration 024) |

Migrations are **idempotent** (`CREATE TABLE IF NOT EXISTS`, `ALTER` guarded)
and applied automatically at boot — no separate deploy step required.

### 5.2 The three save representations

1. **Versioned snapshot store (JWT world state)** — `PUT /worlds/:id/state`
   stores the full `WorldStateData` document **gzip-compressed** as a new
   version; `GET`/`sync` decompress. Diffs between adjacent versions live in
   `world_state_diffs` (also gzip); if any diff in a range is missing the
   server silently serves a full snapshot instead (self-healing).
2. **Opaque client blob (Unity `/sync/state`)** — the client's own
   `BubbleWorldSaveData` JSON as a string. Backend owns only
   `version + updatedAt + durability`; the shape belongs to the client.
   Stored in `client_world_states`, cached in-memory (§5.4).
3. **Relational projection** — entities, tiles, soulform, AP, events live in
   normal tables for querying and per-system mutation (tick, actions).

Serialization convention: dictionary-keyed collections (`cells`,
`tileStates`, `tileCompositions`) are keyed by `"x,y,z"` to match Unity's
`Record<string,T>` — no positional-array ambiguity.

### 5.3 Version & journal model

```
PUT /state (owner) ──▶ snapshot v N+1 (gzip) + worlds.current_version bump
POST /actions       ──▶ action_journal_entries (accepted|rejected, per item)
                         applied inside ONE transaction (BEGIN/COMMIT)
POST /tick          ──▶ SELECT … FOR UPDATE on worlds row → day/season/weather/AP
                         → current_version +1
GET  /sync?since=ckpt ─▶ diffs if complete range, else full snapshot
```

- The action journal is an append-only audit of every client intent with the
  server's verdict (`serverResult`), enabling replay/debug and anti-cheat
  analysis.
- `POST /tick` takes a row lock (`FOR UPDATE`) so concurrent ticks
  serialize — two racing ticks cannot double-advance a day.

### 5.4 In-memory world-sync cache

`worldSyncService` holds a `Map<worldKey, snapshot>`:

- **Boot:** every row preloaded before `listen` — the server comes up already
  holding authoritative state.
- **Reads:** served from the map (hits `GET /sync/state` hot path).
- **Writes:** pass through to Postgres, then refresh the map.
- **Failure:** preload failure is non-fatal; cache miss falls back to DB.

⚠️ This cache is **per process** — it is correct only for a single task/instance.
See the pre-scaling checklist in §13.9.

---

## 6. Auth & authorization

### 6.1 Three models

```
Model A — JWT Bearer            Model B — shared x-api-key       Model C — public
auth · players · worlds         /ai/chat · /sync/state           GET /health
entities · companion            /pairing/device/*                GET /ai/status
game-data · pairing (JWT)                                      
│                                │                               
├ access token 15m (JWT)         ├ value = BRAIN_API_KEY          
├ refresh token 7d (hashed       ├ timing-safe compare → 401      
│  in sessions table,            ├ skipped if key not configured  
│  rotated on /refresh)          │  (local dev only)              
└ deviceId claim → req.deviceId  └ no user identity at all        
```

### 6.2 World authorization

`requireWorldAccess(minRole)` runs a DB check per request:

```
owner (worlds.owner_account_id) → passes everything
else world_members row → role ∈ {viewer(0), member(1), owner(2)}
   hierarchy ≥ minRole? → 403 otherwise
world missing → 404 · no token → 401
```

Reads need `viewer`, gameplay writes `member`, destructive/authoritative
operations (`PUT state`, `tick`, entity delete, bubble create, soulform
PATCH) need `owner`. A non-member probing someone else's world gets `403`
(world exists) — verified by the cross-player smoke test.

`game-data` routes do the same check inline (documented in `API.md` §13).

### 6.3 Pairing trust

- Device sessions (created by the login-less Unity client) carry **no owner
  account** — `pairing_sessions.account_id/world_id` are nullable and the
  link is the `world_key` column (migration 024).
- Claim is **single-use** (`status: pending → claimed` inside a transaction),
  **expires after 5 minutes**, rate-limited to 10/hour, and the JWT-side token
  is stored **hashed** (sha256) — a DB leak does not yield claimable codes.
- Claiming returns the `worldKey`, which becomes the companion app's key for
  `/sync/state`; `GET /pairing/linked` reads the set back.

---

## 7. Core flows

### 7.1 Soulform brain turn (Unity → backend → Bedrock)

```
Unity SoulformBrainService        Backend (ai.ts + bedrockService)      Bedrock
─────────────────────────         ───────────────────────────────      ───────
EnsureResolved() ── GET /ai/status (local link, then remote; 2s each)
build worldContext string
POST /ai/chat ─────────────────▶  requireSharedKey (x-api-key)
 {worldContext, message?}          chatLimiter 30/min + Zod (≤20k ctx)
                                   build prompt (backend owns format)
                                   SigV4 sign → invoke llama3-1-8b
                                   3 hallucination gates ⟲ retries
                                      │ success
                                      ▼
◀──── FLAT game JSON ───────────  {currentThought, mood, targetX/Y/Z(str|null),
 ParseResponse + client gates       expression, animation, action, model,
 (independent 2nd opinion)           attempts, safetyStatus}
 mock fallback if unreachable     mock fallback if Bedrock not configured
```

- `message` omitted → treated as a **brain tick** (different retry budget).
- `model:"mock"` tells the client a canned answer was served — the game keeps
  running offline; connection errors `Invalidate()` the resolved link so the
  next attempt fails over local ↔ remote.

### 7.2 World save sync (Unity ↔ backend)

```
Startup:  GET /sync/state?worldKey=default ──▶ version/state
          state == null → keep local save (backend reachable, nothing stored)
          state != null → server snapshot WINS, replace local

Save:     PUT /sync/state {worldKey, state: "<BubbleWorldSaveData JSON string>"}
          backend validates JSON.parse, stores v+1, returns new version
```

JWT clients (companion/admin) read the same data through
`GET /worlds/:id/state` and `GET /worlds/:id/sync?since=` (diff/full).

### 7.3 QR pairing (Switch → phone)

```
Switch (PairingService)             Backend                    Phone (companion)
POST /pairing/device/session ─────▶ create pending row
 ◀── sessionId, shortCode(6),        expiry = now + 5 min
     deepLink(bubbleworld://…)       
 render QR = deepLink (pure-C#       
 QrCodeEncoder → Texture2D)         
                                     scan ──────────────────▶ POST /pairing/claim {shortCode} (JWT)
                                                             ◀── {worldKey:"default", role:"viewer"}
                                                             GET /pairing/linked ─▶ ["default"]
close overlay ── DELETE /pairing/device/session/:id?worldKey=
                 (code instantly unclaimable; else expires at 5 min)
```

Guarantees: single-use (2nd claim → 404), 5-min expiry (→ 410), wrong
`worldKey` cancel → 404, pairing rate limit 10/h.

### 7.4 Actions, tick, and AP accrual

```
POST /actions (member, ≤60/min)
  for each action: validate actionType ∈ 12 types
    → reject: journal 'rejected' + reason
    → accept: applyAction() → journal 'accepted' + serverResult
  ONE transaction for the batch — partial application impossible

POST /tick (owner)  → FOR UPDATE lock → +1 day → season = floor((day/30)%4)
  → weather roll → passive AP grant → soulform/entity/source-logic ticks
  → version +1

GET /ap → balance + lastPassiveAPTime — accrual computed from SERVER time,
          never a client clock (acceptance criterion)
```

AP spending (interventions 30 AP default, companion chat 1–3 AP by length)
goes through `simulationService.spendAP/grantAP` which write both
`ap_balances` and the append-only `architect_point_ledger`.

### 7.5 Companion chat (JWT)

```
POST /companion/:id/chat {text, sessionId?}  (member, ≤30/min)
  calculateChatCost (≤20w→1, ≤50w→2, else 3 AP) → spendAP
  worldContextService builds context → bedrockService.queryBrain
  persist player + soulform messages (new chat session if none given)
  ◀── 201 {playerMessage, soulformMessage, brainResponse}
```

---

## 8. Consistency & authority rules

| Rule | Enforcement |
|---|---|
| Server wins every conflict | Snapshots versioned server-side; client PUT overwrites as a new version; `/sync` pull replaces local state |
| AP never goes negative / never double-grants | `spendAP`/`grantAP` update balance + ledger in one statement path; accrual key `lastPassiveAPTime` is server clock |
| Day cannot double-advance | `SELECT … FOR UPDATE` row lock in `processTick` |
| Action batches are all-or-nothing | single transaction per batch; per-action verdicts journaled |
| Pairing code usable exactly once | conditional `UPDATE … WHERE status='pending'` inside a transaction; second claim matches 0 rows → 404 |
| Pairing code short-lived | `expires_at` checked at claim → 410 + row marked `expired` |
| Secrets compared without timing leaks | `crypto.timingSafeEqual` on Buffers for `x-api-key` |
| Refresh tokens not reusable after rotation | only a bcrypt hash stored; `/refresh` issues a new pair |
| Coordinates validated | tile x/y/z parsed with `isNaN` → 400 before any SQL |

---

## 9. Reliability

- **Brain:** 3-tier fallback — Bedrock → retries with rewrite note →
  self-corrected response → canned mock. Gameplay never blocks on AWS.
- **Sync cache:** boot preload failure non-fatal; cache miss → DB.
- **Client failover:** `BackendGateway` probes local then remote link,
  caches the winner, `Invalidate()`s on connection errors — a backend
  restarted later is picked up on the next attempt.
- **Diff self-healing:** incomplete diff ranges degrade to a full snapshot.
- **Structured failures:** `AppError` (operational) vs unhandled (500 +
  logged); dev responses include stack traces.
- **Process crash:** migrations + preload re-run on next boot; state is
  entirely in Postgres (no write-ahead state lost).

---

## 10. Security posture

**In place**

- `helmet` headers, CORS (wildcard only in dev), `compression`, 50 MB body
  cap, `trust proxy = 1` (correct client IPs behind an ALB — required for
  rate limiting to work on AWS).
- Zod validation on every mutating body; enum whitelists (entity types,
  action types, expression/animation/action vocabularies for the LLM).
- JWT auth with short access expiry; hashed refresh tokens; bcrypt (12 rounds).
- Timing-safe shared-key comparison; pairing token stored hashed; short-code
  brute force capped by the 10/h pairing limiter.
- Per-route rate limits (auth 20/15min, chat 30/min, actions 60/min).
- LLM output gatekeeping server-side (allowed-expression lists) + a second,
  independent client-side hallucination check.

**Known gaps (documented, fix before production)**

| Gap | Risk | Fix |
|---|---|---|
| Static AWS keys in `.env` (`AKIA…`) | key leak = Bedrock account compromise | IAM task role (§13.6) + delete keys from `.env` |
| `JWT_SECRET` default `change-me-in-production` | forged tokens | inject from secrets store, generate 256-bit value |
| `GET /ai/status` unauthenticated | reveals model/region/config | add shared-key or ignore (low sensitivity) |
| CORS `origin: '*'` when `isDev` | dev-only, but `isDev` = any non-`production` NODE_ENV | ensure `NODE_ENV=production` on deploy |
| Rate limits are in-memory (per process) | limits multiply by task count | Redis-backed store before scaling out (§13.5) |

---

## 11. Configuration & operations

### 11.1 Config groups (`src/config.ts`, all from `.env`)

| Group | Keys (defaults) |
|---|---|
| Core | `PORT=3000`, `NODE_ENV` |
| Database | `DATABASE_URL`, `DATABASE_POOL_MIN=2`, `DATABASE_POOL_MAX=10` |
| Redis | `REDIS_URL` *(unused today)* |
| JWT | `JWT_SECRET`, `JWT_ACCESS_EXPIRY=15m`, `JWT_REFRESH_EXPIRY=7d` |
| Pairing | `PAIRING_CODE_EXPIRY_MINUTES=5`, `PAIRING_RATE_LIMIT_PER_HOUR=10` |
| Rate limit | `RATE_LIMIT_WINDOW_MS=60000`, `RATE_LIMIT_MAX=100` |
| Economy | `AP_PASSIVE_INTERVAL_HOURS=24`, `AP_DAILY_PASSIVE_AMOUNT=1`, `AP_STARTING_BALANCE=100`, `AP_INTERVENTION_COST=30` |
| Bedrock | `BEDROCK_REGION=us-east-1`, `BEDROCK_ACCESS_KEY_ID`, `BEDROCK_SECRET_ACCESS_KEY`, `BEDROCK_MODEL_ID`, `BEDROCK_MAX_GEN_LEN=250`, `BEDROCK_TEMPERATURE=0.7`, `BEDROCK_TOP_P=0.9`, `BEDROCK_TIMEOUT_MS=30000` |
| Brain | `BRAIN_MAX_RETRIES=2`, `BRAIN_TICK_MAX_RETRIES=1`, `BRAIN_TICK_SECONDS=5`, `BRAIN_API_KEY` |

### 11.2 Scripts

| Script | Purpose |
|---|---|
| `npm run dev` | nodemon + `tsx` live reload (port 3000) |
| `npm run build` | `tsc` → `dist/` |
| `npm start` | `node dist/index.js` (production entry) |
| `npm run migrate` | run migrations standalone (also runs at boot) |
| `smoke-test.ps1` | 14-check end-to-end regression |
| `test-device-pairing.ps1` | 11-check pairing suite |

Verification baseline: `npx tsc --noEmit` → 0 errors;
`smoke-test.ps1` 14/14 PASS; `test-device-pairing.ps1` 11/11 PASS against
local Postgres 14.

---

## 12. Current limitations

1. **Single-process assumption** — world-sync cache and rate-limit counters
   are in-memory; two tasks would (a) serve stale snapshots to each other and
   (b) double the effective rate limits. Not safe to scale out before §13.5.
2. **Redis declared but unwired** — `ioredis`/`redis` in `package.json`,
   `REDIS_URL` in config, zero imports in `src/`.
3. **No real-time channel** — all state transfer is request/response; the
   planned WebSocket/live sync (P3) is unbuilt.
4. **Static AWS credentials** in `.env` (see §10).
5. **No unit test suite** — verification is two PowerShell E2E scripts +
   TypeScript compilation.
6. **Brain calls block the request** — synchronous 30s Bedrock calls inside
   the HTTP request (acceptable at current scale; a queue or streaming
   response would be the evolution).
7. **Tick is client-driven** — `POST /tick` fires when a client asks; there
   is no server scheduler (fine for now because AP accrual is computed
   lazily from server time on read).

---

## 13. AWS deployment recommendation

### 13.1 Headline recommendation

> **ECS Fargate + ALB + RDS PostgreSQL + ElastiCache for Redis +
> Secrets Manager + CloudWatch**, with **Amazon Bedrock** (already integrated)
> and **ECR + GitHub Actions** for delivery — all inside one VPC
> (public ALB subnet, private service/DB subnets, two AZs).

| Concern | Best-suited AWS service | Why this one |
|---|---|---|
| Compute | **ECS Fargate** | Long-lived Express process with a warm in-memory cache and 30s LLM calls; containers are the natural fit; no server management; easy 2-AZ spread |
| Load balancing / TLS | **Application Load Balancer + ACM** | Path/health routing to `/health`, managed cert for the custom domain the Switch trusts, WAF attachable |
| Database | **RDS for PostgreSQL** (Aurora later) | Managed PG with the exact semantics the code uses (`FOR UPDATE`, JSONB, gzip blobs); Multi-AZ failover |
| Cache / pub-sub | **ElastiCache for Redis** | Makes rate limits & world-sync cache cluster-correct; later: WebSocket pub/sub |
| Secrets | **Secrets Manager** (SSM Parameter Store for non-secrets) | Replaces `.env` static keys; IAM-scoped read; optional rotation |
| LLM | **Amazon Bedrock Runtime** (already used) | Native, no servers; IAM task-role SigV4 instead of static keys |
| Images | **Amazon ECR** | Source of truth for task images |
| Logs/metrics | **CloudWatch Logs/Alarms** (+ ALB access logs → S3) | morgan/json already prints structured lines to stdout |
| Delivery | **GitHub Actions → ECR → ECS rolling deploy** | Simplest path for this repo; migrations run as a pre-deploy ECS task |
| Edge (future) | Route 53 (+ optional CloudFront for a companion web app) | Custom domain, health checks, static hosting |

### 13.2 Target architecture (VPC)

```
                          players / companion apps
                                   │ HTTPS :443 (ACM cert, game.example.com)
                                   ▼
                     Route 53 (A/alias) ── optional WAF
                                   ▼
        ┌───────────── public subnets (2 AZ) ─────────────┐
        │           Application Load Balancer             │
        │   target group → :3000  health check: /health   │
        │   access logs ──────────────────────▶ S3        │
        │   NAT GW (egress)  ×2  (or VPC endpoints)       │
        └───────────────────────┬─────────────────────────┘
                                ▼
        ┌───────────── private subnets (2 AZ) ────────────┐
        │        ECS Fargate service (desired 2+)         │
        │   container: node dist/index.js  (Express :3000)│
        │   task role: bedrock:InvokeModel (+ logs)       │
        │   secrets: DATABASE_URL, JWT_SECRET,            │
        │            BRAIN_API_KEY, (AWS_SESSION_TOKEN    │
        │            auto-injected with task role)        │
        │                                                 │
        │   ┌──────────────┐        ┌──────────────────┐  │
        │   │ RDS PostgreSQL│        │ ElastiCache Redis│  │
        │   │ Multi-AZ      │        │ (rate limit +    │  │
        │   │ (snapshots,   │        │  sync cache)     │  │
        │   │  journal,     │        └──────────────────┘  │
        │   │  ledger)      │                             │
        │   └──────────────┘                              │
        └─────────────────────────────────────────────────┘
                                │ SigV4 over HTTPS (IAM task role)
                                ▼
                  Amazon Bedrock Runtime · us-east-1
                  (VPC endpoint com.amazonaws.us-east-1.bedrock-runtime
                   optional — keeps LLM traffic off NAT)
```

### 13.3 Compute options compared

| Option | Verdict | Reasoning |
|---|---|---|
| **ECS Fargate** | ✅ **Recommended** | Matches the architecture: long-lived process, warm cache, migrations at boot, health-check based deploys, trivial horizontal scaling once §13.5 is done. ~$0.04/vCPU-h + $0.0044/GB-h |
| **App Runner** | ✅ Good MVP alternative | Least ops (auto-build from ECR, autoscaling, VPC connector to RDS). Same container. Less control over networking/scheduled tasks; slightly higher baseline price |
| **Elastic Beanstalk** | ⚪ Acceptable | Managed EC2 + supervisor for Node; quickest EC2-style path but older model, harder to reason about rolling deploys with migrations |
| **EC2 / Lightsail** | ⚠️ Budget only | Cheapest per-instance, but you own patching, scaling, ALB wiring. Reasonable for a single dev/staging box |
| **Lambda + API Gateway** | ❌ **Not without rework** | Hard mismatches: world-state bodies are up to **20–50 MB** (API Gateway caps at **10 MB**); Bedrock calls run up to 30s × retries (fits timeout but not with warm-cache assumptions); boot-time `preload()` semantics break on cold starts; `express-rate-limit`/sync Map assume one process. Viable only *after* moving all shared state to Redis and capping payloads |

### 13.4 Database

| Stage | Service | Notes |
|---|---|---|
| MVP / now | **RDS PostgreSQL 14/16, db.t4g.micro–small, Single-AZ** | Drop-in: the code targets stock PG. Enable automated backups (7–35 days) |
| Production | **RDS Multi-AZ, db.t4g.medium** | Sub-minute failover; snapshots retained 35 days |
| Growth | **Aurora PostgreSQL (Serverless v2)** | Pay-per-ACU suits spiky game launches; read replicas for companion-app reads; PITR; seamless upsize. Migration is a managed PG → PG move |

Keep the `world_state_snapshots` gzip blobs in Postgres initially — they are
already compressed and read/written transactionally with their metadata. If
snapshots grow into hundreds of MB, move blob payloads to **S3** (key/version
in PG) — but that is a code change, not needed now.

Connection sizing: pool max 10/task × tasks must stay under RDS
`max_connections`; with 2–4 tasks that is fine (default ~100+), but keep
`DATABASE_POOL_MAX` lower (e.g. 5) as tasks grow.

### 13.5 Redis (ElastiCache) — what to wire

Redis is already a dependency; wiring it converts the two single-process
assumptions into cluster-safe behavior:

| Use | Today | With Redis |
|---|---|---|
| Rate-limit counters (`express-rate-limit`) | in-memory per task (limits ×N tasks) | shared store → exact global limits |
| World-sync snapshot cache | per-task `Map` (stale across tasks!) | single shared cache; tasks stop disagreeing |
| (future) WebSocket fan-out | — | pub/sub channels per world (`world:{id}`) |
| (future) pairing short-code cache | DB round-trip | optional hot-path lookup (DB remains source of truth) |

Two deployment shapes:

- **MVP (1 task):** skip Redis entirely — the in-memory implementations are
  correct at exactly one task.
- **≥2 tasks:** ElastiCache Redis (`cache.t4g.micro`, private subnet,
  encryption in transit/at rest) **before** raising `desiredCount`.

### 13.6 Secrets & IAM (important code touchpoint)

1. **Delete static AWS keys from `.env`** (an `AKIA…` key is currently
   committed there). On Fargate, attach an **execution/task role** with
   `bedrock:InvokeModel*` on the model ARN — AWS injects
   `AWS_ACCESS_KEY_ID / AWS_SECRET_ACCESS_KEY / AWS_SESSION_TOKEN`
   automatically.
2. ⚠️ **Small required code change:** `bedrockService.buildSigV4()` signs only
   `host;x-amz-content-sha256;x-amz-date`. Task-role credentials always carry
   a **session token**, which must be included as `x-amz-security-token` in
   both the canonical headers and `SignedHeaders`, and sent as a header.
   Without it, every Bedrock call fails `InvalidToken`. (Alternatively keep
   static keys in Secrets Manager — works, but keeps the long-lived-key risk.)
3. Store `DATABASE_URL`, `JWT_SECRET`, `BRAIN_API_KEY` in **Secrets Manager**;
   inject as task `secrets` (not plain `environment`). Non-secret toggles
   (`BRAIN_TICK_SECONDS`, limits) → **SSM Parameter Store** / task environment.
4. Generate a fresh 256-bit `JWT_SECRET` at provision time — never the
   `change-me-in-production` default.
5. RDS/Redis in private subnets with security groups allowing only the task
   security group; no public DB endpoint.

### 13.7 Networking & the Switch client

- The Nintendo client needs **valid public TLS** → ACM certificate on a real
  domain via Route 53 → ALB (self-signed will not pass on console).
- `app.set('trust proxy', 1)` is already correct for ALB — client IPs (and
  therefore rate limits) stay accurate.
- Optional cost lever: VPC endpoints for `bedrock-runtime` and
  `logs`/`ecr`/`secretsmanager` to cut NAT data-processing charges
  (NAT is the sneaky $30–65/mo line item).
- Keep the Unity **local dev link** (localhost) working: the client already
  fails over local → remote, so developers run the local backend while the
  deployed remote URL is the fallback.

### 13.8 Observability & delivery

- **Logs:** stdout (morgan `dev`/`combined` + service logs) → CloudWatch Logs
  via the awslogs driver; create a metric filter on `"error"`/`500`.
- **Alarms:** ALB 5XX > threshold, target health < 2, CPU > 70%, RDS
  connections/CPU, and a custom alarm on brain latency / `model:"mock"`
  rate (Bedrock degradation is invisible otherwise).
- **Access logs:** ALB → S3 (free beyond storage).
- **Deploy:** GitHub Actions → `docker build` → push ECR → ECS rolling
  update (min 100%, circuit breaker on) with a **pre-deploy migration task**
  (`npm run migrate`) — migrations are idempotent, so boot-time migration in
  the new tasks is also safe; the separate task just avoids a thundering herd.
- **Health check:** `/health` already checks DB connectivity — wire it as the
  ALB target health check (consider failing on `database:"error"` for prod so
  a broken-DB instance is pulled from rotation).

### 13.9 Cost tiers (rough, us-east-1 on-demand, before credits)

| Tier | Shape | Est. monthly |
|---|---|---|
| **MVP / dev** | 1× Fargate (0.5 vCPU/1 GB) or App Runner · RDS db.t4g.micro Single-AZ · no Redis · 1 NAT | **~$50–80** |
| **Production** | ALB + 2× Fargate (1 vCPU/2 GB, 2 AZ) · RDS Multi-AZ db.t4g.medium · ElastiCache micro · Secrets Manager · NAT ×2 or VPC endpoints | **~$250–400** |
| **Scale** | Fargate autoscaling (CPU/ALB req-target) · Aurora Serverless v2 · ElastiCache cluster · WAF · multi-region later | **~$500+**, tracks usage |

Bedrock token cost is separate and usage-based (~$0.0002/input token class
pricing for Llama 3.1 8B — at 200-token prompts, cents per thousand turns).

### 13.10 Pre-scaling checklist (order matters)

Before raising `desiredCount` above 1:

1. Wire Redis-backed `express-rate-limit` store.
2. Move (or drop) the world-sync `Map` cache onto Redis — otherwise tasks
   serve each other stale snapshots.
3. Apply the SigV4 `x-amz-security-token` change and remove static keys.
4. Move secrets out of the image/env into Secrets Manager.
5. Cap `DATABASE_POOL_MAX` for the new task count.
6. Confirm `NODE_ENV=production` (turns off CORS wildcard + stack traces).

---

## 14. Evolution roadmap

| Phase | Change | AWS enabler |
|---|---|---|
| Now | Single-task container deployment | ECS Fargate + RDS + ALB |
| P3 | Real-time sync / live presence (planned WebSocket) | ElastiCache pub/sub + ALB WebSocket support (sticky via Redis) |
| Then | Multi-task backend | §13.10 checklist |
| Growth | Companion reads offload | Aurora read replicas |
| Growth | Async jobs (chat summarization, event rollups, daily tick scheduler) | SQS + scheduled tasks (EventBridge → ECS scheduled run) or Lambda workers |
| Optional | Accounts via managed IdP | Cognito (only if account features expand — current custom JWT is fine) |
| Large saves | Snapshot payloads beyond Postgres comfort | S3 versioned objects, metadata stays in PG |

---

*Document generated from the codebase as of migration `024_pairing_device_sessions`.*
