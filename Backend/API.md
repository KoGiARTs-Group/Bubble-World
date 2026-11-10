# BubbleWorld Backend — API Reference

Complete reference for every HTTP endpoint exposed by the BubbleWorld backend,
with request/response examples and C# usage from the Unity client
(`D:\Unity Projects\BubbleWorld`).

- **Backend project:** `D:\Unity Projects\BubbleWorld_Backend`
- **Route mounts:** `src/index.ts` · **Route handlers:** `src/routes/*.ts`
- **Validation schemas:** `src/middleware/validation.ts`

---

## Table of contents

1. [Overview](#1-overview)
2. [Authentication models](#2-authentication-models)
3. [Conventions](#3-conventions)
4. [Endpoint summary](#4-endpoint-summary)
5. [Unity client integration](#5-unity-client-integration)
6. [Health](#6-health)
7. [Auth](#7-auth)
8. [Players](#8-players)
9. [Worlds](#9-worlds)
10. [Entities, bubbles, tiles](#10-entities-bubbles-tiles)
11. [Pairing](#11-pairing)
12. [Companion](#12-companion)
13. [Game data](#13-game-data)
14. [AI (Soulform brain)](#14-ai-soulform-brain)
15. [Sync (world snapshot)](#15-sync-world-snapshot)
16. [Appendix](#16-appendix)

---

## 1. Overview

| | |
|---|---|
| Base URL (dev) | `http://localhost:3000` |
| API prefix | `/api/v1` (only `GET /health` lives outside it) |
| Content type | `application/json` (request body limit **50 MB**) |
| DB / runtime | PostgreSQL 14 + Node.js/TypeScript + Express 5 |
| LLM | AWS Bedrock (`us.meta.llama3-1-8b-instruct-v1:0`, us-east-1) via native SigV4 |

Every route handler validates input with Zod, checks per-player/per-world
authorization, and returns JSON. World authorization follows the hierarchy
`viewer < member < owner` — a non-member gets `403`, a nonexistent world `404`,
an unauthenticated caller `401`.

---

## 2. Authentication models

Three distinct auth models exist. Each endpoint below states which one it uses.

### 2.1 JWT Bearer (account endpoints)

```http
Authorization: Bearer <accessToken>
```

- Obtained from `/auth/*`. Access token expires in **15 min**, refresh token in **7 days**.
- Used by: `auth`, `players`, `worlds`, `entities`, `pairing` (JWT routes), `companion`, `game-data`.
- The access token may carry a `deviceId` claim; the server reads it from the
  JWT (`req.deviceId`) to tag sync checkpoints and journaled actions — there is
  no separate device header.

### 2.2 Shared key (Unity client, no login)

```http
x-api-key: <BRAIN_API_KEY>
```

- Used by the Unity Switch client on routes it calls **without a login**:
  `POST /ai/chat`, `GET|PUT /sync/state`, `POST|DELETE /pairing/device/session`.
- The value comes from `SoulformBrainConfig.ApiKey` on the client and
  `BRAIN_API_KEY` on the server (`src/middleware/sharedKey.ts`), compared with a
  timing-safe equality check → `401 Invalid or missing x-api-key` on mismatch.
- Enforcement is **skipped when `BRAIN_API_KEY` is not configured** (local dev
  convenience); always set it in production or the routes become open.

### 2.3 Public (no auth)

- `GET /health`
- `GET /api/v1/ai/status` — startup/config probe used by `BackendGateway`.
  *(Known gap: currently unauthenticated; it only reveals model/region/tick config.)*

---

## 3. Conventions

### 3.1 Success envelope

Almost every endpoint answers with:

```json
{ "status": "success", "data": { ... } }
```

**Deliberate exceptions (do not wrap these clientside):**

| Endpoint | Shape |
|---|---|
| `GET /health` | flat `{ status, timestamp, environment, database }` |
| `POST /api/v1/ai/chat` | **flat game JSON** — Unity's `ParseResponse` reads brain fields directly off the body |

Some mutating endpoints return `{ "status": "success", "message": "..." }`
instead of `data` (delete/cancel style routes).

### 3.2 Error envelope

```json
{ "status": "error", "message": "Human readable reason", "details": { ... } }
```

- `details` is present on Zod validation failures (400).
- `stack` is appended in dev mode only.
- Common codes: `400` validation, `401` bad/missing credentials, `403` wrong
  world role, `404` not found / already-used pairing code, `410` expired pairing
  code, `429` rate limited, `500` unexpected.

### 3.3 Rate limits

| Limiter | Window | Max | Applies to |
|---|---|---|---|
| `apiLimiter` | 60 s (`RATE_LIMIT_WINDOW_MS`) | 100 (`RATE_LIMIT_MAX`) | all `/api/v1` routes |
| `authLimiter` | 15 min | 20 | register/login/platform/mobile |
| `pairingLimiter` | 1 hour | 10 (`PAIRING_RATE_LIMIT_PER_HOUR`) | pairing create/claim/device-session |
| `chatLimiter` | 60 s | 30 | companion chat, `/ai/chat` |
| `actionLimiter` | 60 s | 60 | world actions, interventions |

Rate-limit responses are `429` with the standard `RateLimit-*` headers.

### 3.4 Field naming

- **camelCase** for typed DTO responses (world state, entities, brain, live, chat, pairing).
- **snake_case** for raw SQL row returns: `GET /worlds` list rows,
  `worlds` create `RETURNING *`, `GET .../soulform`, `GET .../events`.
  Unity's `JsonUtility` binds by exact field name — use the right DTO shape.

### 3.5 World roles

| Role | Granted by | Can |
|---|---|---|
| `owner` | world creator | PUT state, tick, delete entities, create bubbles, PATCH soulform, cancel pairing |
| `member` | `world_members` row | submit actions, spawn/move entities, chat, interventions, tiles |
| `viewer` | `world_members` row | read-only: state, sync, live, entities, memories |

---

## 4. Endpoint summary

| Method | Path | Auth | Min role | Description |
|---|---|---|---|---|
| GET | `/health` | none | — | Server + DB health |
| POST | `/api/v1/auth/register` | none | — | Email/password registration |
| POST | `/api/v1/auth/login` | none | — | Email/password login |
| POST | `/api/v1/auth/platform/nintendo` | none | — | Platform (console) sign-in / account link |
| POST | `/api/v1/auth/mobile` | none | — | Mobile login (+device fields) |
| POST | `/api/v1/auth/refresh` | none | — | Rotate refresh token → new pair |
| POST | `/api/v1/auth/logout` | Bearer* | — | Revoke session |
| GET | `/api/v1/players/me` | Bearer | — | Full player profile |
| PATCH | `/api/v1/players/me` | Bearer | — | Update profile |
| POST | `/api/v1/players/devices` | Bearer | — | Register/update device |
| GET | `/api/v1/worlds` | Bearer | — | Worlds owned + shared |
| POST | `/api/v1/worlds` | Bearer | — | Create world |
| GET | `/api/v1/worlds/:worldId/state` | Bearer | viewer | Full world state snapshot |
| PUT | `/api/v1/worlds/:worldId/state` | Bearer | owner | Upload world state (new version) |
| GET | `/api/v1/worlds/:worldId/sync` | Bearer | viewer | Incremental sync (diff or full) |
| POST | `/api/v1/worlds/:worldId/actions` | Bearer | member | Submit gameplay action batch |
| POST | `/api/v1/worlds/:worldId/tick` | Bearer | owner | Advance simulation (day/weather/AP) |
| GET | `/api/v1/worlds/:worldId/live` | Bearer | viewer | Live world summary |
| POST | `/api/v1/worlds/:worldId/interventions` | Bearer | member | Spend AP on an intervention |
| GET | `/api/v1/worlds/:worldId/ap` | Bearer | viewer | AP balance |
| GET | `/api/v1/worlds/:worldId/entities` | Bearer | viewer | List entities (`?type=`) |
| GET | `/api/v1/worlds/:worldId/entities/:entityId` | Bearer | viewer | Get one entity |
| POST | `/api/v1/worlds/:worldId/entities` | Bearer | member | Spawn NPC/Animal/Soulform |
| PATCH | `/api/v1/worlds/:worldId/entities/:entityId` | Bearer | member | Update entity |
| DELETE | `/api/v1/worlds/:worldId/entities/:entityId` | Bearer | owner | Remove entity |
| GET | `/api/v1/worlds/:worldId/bubbles` | Bearer | viewer | List bubbles |
| POST | `/api/v1/worlds/:worldId/bubbles` | Bearer | owner | Create bubble |
| GET | `/api/v1/worlds/:worldId/tiles/:bubbleId` | Bearer | viewer | Tile cells of a bubble |
| PUT | `/api/v1/worlds/:worldId/tiles/:bubbleId/:x/:y/:z` | Bearer | member | Set one tile cell |
| DELETE | `/api/v1/worlds/:worldId/tiles/:bubbleId/:x/:y/:z` | Bearer | member | Remove one tile cell |
| POST | `/api/v1/pairing/session` | Bearer | — | Create pairing session (JWT side) |
| POST | `/api/v1/pairing/claim` | Bearer | — | Claim a pairing short code |
| DELETE | `/api/v1/pairing/session/:id` | Bearer | — | Cancel own pending session |
| POST | `/api/v1/pairing/device/session` | x-api-key | — | **Unity:** create QR pairing session |
| DELETE | `/api/v1/pairing/device/session/:id` | x-api-key | — | **Unity:** cancel pending session |
| GET | `/api/v1/pairing/linked` | Bearer | — | Worlds linked via device claims |
| POST | `/api/v1/companion/:worldId/chat` | Bearer | member | Companion chat (AP-charged) |
| GET | `/api/v1/companion/:worldId/chat` | Bearer | viewer | Chat sessions + messages |
| GET | `/api/v1/companion/:worldId/memories` | Bearer | viewer | Memory chapters |
| POST | `/api/v1/game-data/:worldId/soulform/brain` | Bearer | member† | Brain query (JWT envelope) |
| GET | `/api/v1/game-data/:worldId/soulform` | Bearer | viewer† | Soulform state row |
| PATCH | `/api/v1/game-data/:worldId/soulform` | Bearer | owner | Patch soulform fields |
| GET | `/api/v1/game-data/:worldId/events` | Bearer | viewer† | World event log |
| POST | `/api/v1/game-data/:worldId/events` | Bearer | owner | Append world event |
| POST | `/api/v1/ai/chat` | x-api-key | — | **Unity:** brain chat (flat response) |
| GET | `/api/v1/ai/status` | none | — | Brain/backend runtime config |
| GET | `/api/v1/sync/state` | x-api-key | — | **Unity:** pull snapshot |
| PUT | `/api/v1/sync/state` | x-api-key | — | **Unity:** push snapshot |

† `game-data` routes use an inline check instead of `requireWorldAccess`: any
world member (including `viewer`) passes reads; `mode=chat` brain queries reject
`viewer` (403); soulform `PATCH` and events `POST` are owner-only.

\* `POST /auth/logout` accepts a token even if expired (best-effort revoke).

---

## 5. Unity client integration

### 5.1 Architecture

```
┌────────────────────────── Unity (Switch/PC) ──────────────────────────┐
│ BackendGateway        link resolution + ApiKey + served config       │
│ ServerSyncService     GET|PUT /sync/state                            │
│ SoulformBrainService  POST /ai/chat  (worldContext string only)      │
│ PairingService        POST|DELETE /pairing/device/session + QR image │
└───────────────────────────────┬───────────────────────────────────────┘
                                │ HTTPS/JSON (+ x-api-key, no login)
┌───────────────────────────────▼───────────────────────────────────────┐
│ Express 5 (api/v1) → Zod validation → auth / sharedKey / worldAccess │
│ → services (Postgres, Redis) → Bedrock (SigV4) for brain routes      │
└───────────────────────────────────────────────────────────────────────┘
```

Relevant Unity files:

| File | Role |
|---|---|
| `Assets/Scripts/BubbleWorld/Services/BackendGateway.cs` | Probes `GET /ai/status` on local link first, then remote (2 s each); first 200 wins, config applied, base cached; `Invalidate()` on connection errors → failover |
| `Assets/Scripts/BubbleWorld/Services/ServerSyncService.cs` | Snapshot pull/push (`worldKey=default`) |
| `Assets/Scripts/BubbleWorld/Services/SoulformBrainService.cs` | Builds `worldContext`, calls `/ai/chat`, runs client-side hallucination checks |
| `Assets/Scripts/BubbleWorld/Pairing/PairingService.cs` | Device pairing session lifecycle + QR texture |
| `Assets/Scripts/BubbleWorld/Pairing/PairingScreenUI.cs` | Runtime-built pairing overlay (QR, code, countdown) |

### 5.2 Generic C# request helper

All Unity calls follow this coroutine pattern (same style the existing
services use). Use it for the JWT endpoints — the Unity client currently only
implements the shared-key routes itself:

```csharp
using System;
using System.Collections;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

public static class BubbleApi
{
    public const string Base = "http://localhost:3000"; // or BackendGateway.ResolvedBase

    public static IEnumerator Send(
        string method, string path, string json,
        string bearer,            // JWT or null
        string apiKey,            // shared key or null
        Action<string> onOk,      // raw response body
        Action<long, string> onErr)
    {
        using (var req = new UnityWebRequest(Base + path, method))
        {
            if (json != null)
            {
                req.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(json));
                req.downloadHandler = new DownloadHandlerBuffer();
                req.SetRequestHeader("Content-Type", "application/json");
            }
            else
            {
                req.downloadHandler = new DownloadHandlerBuffer();
            }
            if (!string.IsNullOrEmpty(bearer)) req.SetRequestHeader("Authorization", "Bearer " + bearer);
            if (!string.IsNullOrEmpty(apiKey)) req.SetRequestHeader("x-api-key", apiKey);
            req.timeout = 30;

            yield return req.SendWebRequest();

            if (req.result == UnityWebRequest.Result.Success)
                onOk?.Invoke(req.downloadHandler.text);
            else
                onErr?.Invoke(req.responseCode, req.downloadHandler.text);
        }
    }
}
```

Envelope handling:

```csharp
[Serializable] class Envelope<T> { public T data; }

void OnOk(string json)
{
    var env = JsonUtility.FromJson<Envelope<WorldList>>(json);
    if (env?.data == null) { /* error shape: parse {"status":"error","message":...} */ }
}
```

> **Note:** `POST /api/v1/ai/chat` is intentionally **not** wrapped in an
> envelope — parse it directly (see §14).

---

## 6. Health

### `GET /health` — public

Liveness + DB check. Used by ops scripts.

**Example**

```http
GET /health
```

**200 response**

```json
{
  "status": "ok",
  "timestamp": "2026-10-04T09:12:44.512Z",
  "environment": "development",
  "database": "ok"
}
```

`database` becomes `"error"` if Postgres is unreachable (status stays 200).

**C#**

```csharp
yield return BubbleApi.Send("GET", "/health", null, null, null,
    json => Debug.Log(json),
    (code, err) => Debug.LogWarning("health failed: " + code));
```

---

## 7. Auth

All auth endpoints sit behind `authLimiter` (20 requests / 15 min) except
`refresh`/`logout`.

### `POST /api/v1/auth/register` — public

Creates an account and returns the first token pair.

**Request**

```json
{
  "email": "player@example.com",
  "password": "Test1234!",
  "displayName": "Aria"
}
```

| Field | Rules |
|---|---|
| `email` | valid email (required in practice) |
| `password` | 8–128 chars |
| `displayName` | 1–100 chars (required) |

**201 response**

```json
{
  "status": "success",
  "data": {
    "account": { "id": "0b1f…uuid", "displayName": "Aria" },
    "tokens": {
      "accessToken": "eyJhbGciOi…",
      "refreshToken": "eyJhbGciOi…"
    }
  }
}
```

**C#**

```csharp
string body = JsonUtility.ToJson(new {
    email = "player@example.com", password = "Test1234!", displayName = "Aria"
});   // anonymous types need a [Serializable] class with JsonUtility — see note below

yield return BubbleApi.Send("POST", "/api/v1/auth/register", body, null, null,
    json => { var e = JsonUtility.FromJson<AuthEnvelope>(json);
              PlayerPrefs.SetString("access", e.data.tokens.accessToken);
              PlayerPrefs.SetString("refresh", e.data.tokens.refreshToken); },
    (code, err) => Debug.LogError(code + " " + err));
```

with

```csharp
[Serializable] class AuthEnvelope { public AuthData data; }
[Serializable] class AuthData { public AccountInfo account; public Tokens tokens; }
[Serializable] class AccountInfo { public string id; public string displayName; }
[Serializable] class Tokens { public string accessToken; public string refreshToken; }
```

*(JsonUtility cannot serialize anonymous objects — define request classes too.)*

---

### `POST /api/v1/auth/login` — public

**Request**

```json
{ "email": "player@example.com", "password": "Test1234!" }
```

**200 response** — same shape as register (without `created`).

Errors: `401 Invalid credentials`.

---

### `POST /api/v1/auth/platform/nintendo` — public

Console sign-in: creates the account on first use, links on subsequent calls.

**Request**

```json
{
  "platform": "nintendo",
  "platformUserId": "nsaid-778899",
  "platformToken": "optional-session-token",
  "displayName": "SwitchPlayer"
}
```

`platform` ∈ `nintendo | mobile | web`; `platformUserId` 1–255 chars.

**200/201 response**

```json
{
  "status": "success",
  "data": {
    "account": { "id": "…", "displayName": "SwitchPlayer" },
    "tokens": { "accessToken": "…", "refreshToken": "…" },
    "created": true
  }
}
```

`201` when the account was created, `200` when an existing account was linked.

---

### `POST /api/v1/auth/mobile` — public

Email/password login with optional device fields (accepted for telemetry).

**Request**

```json
{
  "email": "player@example.com",
  "password": "Test1234!",
  "deviceType": "iOS",
  "deviceId": "iphone-abc",
  "deviceName": "Aria's iPhone"
}
```

**200 response** — `{ account, tokens }` (same as login).

---

### `POST /api/v1/auth/refresh` — public

Rotates the refresh token; old refresh token is invalidated.

**Request**

```json
{ "refreshToken": "eyJhbGciOi…" }
```

**200 response**

```json
{
  "status": "success",
  "data": {
    "tokens": { "accessToken": "…", "refreshToken": "…" }
  }
}
```

**C# token refresh helper**

```csharp
public static IEnumerator Refresh(Action<string> onNewAccess, Action onLoggedOut)
{
    string body = JsonUtility.ToJson(new RefreshReq { refreshToken = PlayerPrefs.GetString("refresh") });
    yield return BubbleApi.Send("POST", "/api/v1/auth/refresh", body, null, null,
        json => {
            var e = JsonUtility.FromJson<TokensEnvelope>(json);
            PlayerPrefs.SetString("access", e.data.tokens.accessToken);
            PlayerPrefs.SetString("refresh", e.data.tokens.refreshToken);
            onNewAccess?.Invoke(e.data.tokens.accessToken);
        },
        (code, err) => onLoggedOut?.Invoke());   // 401 → session dead
}
[Serializable] class RefreshReq { public string refreshToken; }
[Serializable] class TokensEnvelope { public TokensData data; }
[Serializable] class TokensData { public Tokens tokens; }
```

---

### `POST /api/v1/auth/logout` — Bearer (best-effort)

**Request**

```http
POST /api/v1/auth/logout
Authorization: Bearer <accessToken>
Content-Type: application/json

{ "refreshToken": "eyJ…" }     // optional
```

**200 response**

```json
{ "status": "success", "message": "Logged out" }
```

An expired access token is tolerated — logout still succeeds.

---

## 8. Players

### `GET /api/v1/players/me` — Bearer

Full profile: account, preferences, lifetime stats, platforms, devices.

**200 response**

```json
{
  "status": "success",
  "data": {
    "account": {
      "id": "…uuid",
      "email": "player@example.com",
      "displayName": "Aria",
      "createdAt": "2026-09-01T12:00:00.000Z"
    },
    "profile": { "display_name": "Aria", "region": "EU", "preferences": {}, "consent_flags": {} },
    "stats": {
      "total_play_time_seconds": 5400,
      "total_days_played": 3,
      "worlds_created": 1,
      "total_population": 4,
      "skills_unlocked": 2,
      "skills_mastered": 0,
      "interventions_used": 1,
      "chat_messages_sent": 17,
      "offline_syncs": 0
    },
    "platforms": [],
    "devices": [
      { "id": "…", "type": "Switch", "name": "Living room", "isTrusted": true, "lastSeenAt": "2026-10-04T08:00:00.000Z" }
    ]
  }
}
```

### `PATCH /api/v1/players/me` — Bearer

**Request** (all fields optional)

```json
{
  "displayName": "AriaX",
  "region": "EU",
  "preferences": { "reducedMotion": true },
  "consentFlags": { "analytics": false }
}
```

**200 response** — same profile shape as `GET /players/me`.

### `POST /api/v1/players/devices` — Bearer

Upserts by `(accountId, deviceId)` (conflict updates `last_seen_at`).

**Request**

```json
{ "deviceType": "Switch", "deviceId": "switch-001", "deviceName": "Living room" }
```

**201 response**

```json
{
  "status": "success",
  "data": {
    "id": "…uuid",
    "type": "Switch",
    "deviceId": "switch-001",
    "name": "Living room",
    "isTrusted": true,
    "lastSeenAt": "2026-10-04T09:20:00.000Z"
  }
}
```

**C#**

```csharp
string jwt = PlayerPrefs.GetString("access");
string body = JsonUtility.ToJson(new DeviceReq { deviceType = "Switch", deviceId = SystemInfo.deviceUniqueIdentifier });
yield return BubbleApi.Send("POST", "/api/v1/players/devices", body, jwt, null, Debug.Log, Err);
[Serializable] class DeviceReq { public string deviceType; public string deviceId; }
```

---

## 9. Worlds

All world routes require Bearer auth **and** a world role (see §3.5).

### `GET /api/v1/worlds` — Bearer

Owned worlds first (role `owner`), then shared worlds.

**200 response** — rows are raw SQL columns (**snake_case**):

```json
{
  "status": "success",
  "data": {
    "worlds": [
      {
        "id": "…uuid",
        "name": "SmokeWorld",
        "environment_type": "Forest",
        "current_version": 4,
        "day_count": 3,
        "current_season": 0,
        "weather_condition": "Clear",
        "livability_percent": 62,
        "updated_at": "2026-10-04T09:00:00.000Z",
        "role": "owner"
      }
    ]
  }
}
```

**C#**

```csharp
[Serializable] class WorldsEnv { public WorldRow[] worlds; }
[Serializable] class WorldRow {
    public string id; public string name; public string environment_type;
    public int current_version; public int day_count; public int current_season;
    public string weather_condition; public int livability_percent;
    public string updated_at; public string role;
}

yield return BubbleApi.Send("GET", "/api/v1/worlds", null, jwt, null,
    json => {
        var env = JsonUtility.FromJson<Envelope<WorldsEnv>>(json);
        foreach (var w in env.data.worlds) Debug.Log($"{w.name} v{w.current_version} [{w.role}]");
    },
    (c, e) => Debug.LogError(e));
```

### `POST /api/v1/worlds` — Bearer

Creates world + starting AP balance (100) + soulform row + chapter 0.

**Request**

```json
{
  "name": "SmokeWorld",
  "environmentType": "Forest",
  "gridWidth": 60,
  "gridHeight": 60,
  "gridDepth": 8
}
```

| Field | Rules | Default |
|---|---|---|
| `name` | 1–255, required | — |
| `environmentType` | `MeadowGrassland \| Forest \| BeachCoastal` | `MeadowGrassland` |
| `gridWidth` / `gridHeight` | int 10–200 | 60 |
| `gridDepth` | int 1–30 | 8 |

**201 response** — `world` is the raw `worlds` row (`RETURNING *`, snake_case),
e.g.:

```json
{
  "status": "success",
  "data": {
    "world": {
      "id": "…uuid",
      "owner_account_id": "…uuid",
      "name": "SmokeWorld",
      "environment_type": "Forest",
      "grid_width": 60, "grid_height": 60, "grid_depth": 8,
      "current_version": 0, "day_count": 1, "current_season": 0,
      "weather_condition": null, "livability_percent": 0,
      "created_at": "…", "updated_at": "…"
    }
  }
}
```

### `GET /api/v1/worlds/:worldId/state` — viewer

Full authoritative snapshot (latest compressed snapshot, or DB-built fallback
for a fresh world).

**200 response** — `state` is the `WorldStateData` document (camelCase):

```json
{
  "status": "success",
  "data": {
    "state": {
      "saveVersion": "2.1",
      "worldName": "SmokeWorld",
      "environmentType": "Forest",
      "gridWidth": 60, "gridHeight": 60, "gridDepth": 8,
      "cells": { "0,0,0": { "…": "…" } },
      "livabilityPercent": 62,
      "weatherCondition": "Clear",
      "soulform": { "name": "SmokeSoul" },
      "dayCount": 3,
      "currentSeason": 0,
      "architectPoints": 140,
      "tileCompositions": {},
      "tileStates": {},
      "cooldowns": [],
      "entities": [],
      "bubbles": [],
      "memoryChapters": "[]",
      "skillData": "{}"
    }
  }
}
```

> Dictionary-shaped collections (`cells`, `tileStates`, `tileCompositions`,
> `soulform`) are keyed by `"x,y,z"` (or field name), matching the Unity
> dictionary-keyed serialization.

### `PUT /api/v1/worlds/:worldId/state` — owner

Upload a full snapshot; server stores it as a new version (server wins all
conflicts) and returns the new checkpoint.

**Request** (Zod `uploadWorldState`; everything has a default except `worldName`)

```json
{
  "saveVersion": "2.1",
  "worldName": "SmokeWorld",
  "environmentType": "Forest",
  "gridWidth": 60, "gridHeight": 60, "gridDepth": 8,
  "cells": {},
  "livabilityPercent": 62,
  "weatherCondition": "Clear",
  "soulform": { "name": "SmokeSoul" },
  "dayCount": 1,
  "currentSeason": 0,
  "architectPoints": 100,
  "tileCompositions": {},
  "tileStates": {},
  "cooldowns": [],
  "entities": [],
  "bubbles": [],
  "memoryChapters": "[]",
  "skillData": "{}"
}
```

**200 response**

```json
{ "status": "success", "data": { "version": 5, "checkpoint": "ckpt-…" } }
```

### `GET /api/v1/worlds/:worldId/sync?since=<checkpoint>` — viewer

Delta sync. `since` omitted/unknown → full snapshot; known checkpoint → merged
diffs (falls back to full if any diff is missing).

**200 response (full)**

```json
{
  "status": "success",
  "data": {
    "type": "full",
    "data": { "…WorldStateData…": "…" },
    "version": 5,
    "checkpoint": "ckpt-…"
  }
}
```

**200 response (no changes)**

```json
{
  "status": "success",
  "data": { "type": "diff", "data": { "changes": [] }, "version": 5, "checkpoint": "ckpt-…" }
}
```

**200 response (diffs)**

```json
{
  "status": "success",
  "data": {
    "type": "diff",
    "data": { "diffs": [ { "fromVersion": 3, "toVersion": 4, "diff": { "…": "…" } } ] },
    "version": 5,
    "checkpoint": "ckpt-…"
  }
}
```

Full responses also record a sync checkpoint for the calling device
(`recordSyncCheckpoint`).

### `POST /api/v1/worlds/:worldId/actions` — member, 60/min

Batch gameplay actions, each validated and journaled
(`action_journal_entries`); applied atomically in one transaction.

**Request**

```json
{
  "actions": [
    {
      "actionType": "place_tile",
      "actionData": { "x": 4, "y": 0, "z": 7, "itemId": "grass_block", "layer": "ground" },
      "clientTimestamp": "2026-10-04T09:21:00.000Z",
      "batchId": "b-001",
      "sequenceIndex": 0
    }
  ]
}
```

- `actions`: 1–500 items.
- Valid `actionType` values:
  `place_tile`, `remove_tile`, `apply_brush`, `intervention`, `entity_spawn`,
  `entity_move`, `soulform_action`, `weather_change`, `day_advance`,
  `chat_message`, `skill_attempt`, `memory_event`.

**200 response** — per-action outcome:

```json
{
  "status": "success",
  "data": {
    "results": [
      { "actionId": "…uuid", "accepted": true, "serverResult": { "placed": true, "x": 4, "y": 0, "z": 7, "itemId": "grass_block", "layer": "ground" } },
      { "actionId": "…uuid", "accepted": false, "reason": "Unknown action type: fly" }
    ]
  }
}
```

Representative `serverResult` payloads: `{placed}`, `{removed}`,
`{brushApplied, cellCount}`, `{interventionApplied, type, apSpent}`,
`{entitySpawned, entityId}`, `{entityMoved, entityId, gridX, gridY, gridZ}`,
`{skillAttempted, skillName, success}`, `{soulformAction}`,
`{weatherChanged, newWeather}`, `{dayAdvanced, newDay, newSeason}`,
`{memoryRecorded}`.

**C#**

```csharp
[Serializable] class ActionBatch { public ActionItem[] actions; }
[Serializable] class ActionItem {
    public string actionType; public ActionDataBlob actionData;
    public string clientTimestamp; public string batchId; public int sequenceIndex;
}
[Serializable] class ActionDataBlob { public int x; public int y; public int z; public string itemId; public string layer; }

var batch = new ActionBatch { actions = new[] { new ActionItem {
    actionType = "place_tile",
    actionData = new ActionDataBlob { x = 4, y = 0, z = 7, itemId = "grass_block", layer = "ground" },
    clientTimestamp = DateTime.UtcNow.ToString("o"),
    batchId = "b-" + Guid.NewGuid().ToString("N"),
    sequenceIndex = 0,
}}};
string body = JsonUtility.ToJson(batch);
yield return BubbleApi.Send("POST", $"/api/v1/worlds/{worldId}/actions", body, jwt, null,
    json => { /* Envelope<Results> → check results[i].accepted */ },
    (c, e) => Debug.LogError(c + " " + e));
```

### `POST /api/v1/worlds/:worldId/tick` — owner

Advances the simulation: +1 day, season recompute `floor((day/30) % 4)`,
weather roll, passive AP grant, soulform/entity/source-logic ticks, version bump.

**Request**

```json
{ "tickType": "offline_catchup", "offlineDurationHours": 9.5 }
```

| Field | Rules | Default |
|---|---|---|
| `tickType` | `scheduled \| requested \| offline_catchup` | `requested` |
| `offlineDurationHours` | 0–168 (only meaningful for `offline_catchup`) | — |

**200 response**

```json
{
  "status": "success",
  "data": {
    "tickResult": {
      "tickType": "offline_catchup",
      "previousDay": 3,
      "offlineCatchup": { "daysAdvanced": 1, "eventsProcessed": 1 },
      "dayAdvanced": { "previousDay": 3, "newDay": 4, "newSeason": 0 },
      "weatherRoll": { "newWeather": "Rain", "changed": true },
      "passiveAP": { "granted": 10, "newBalance": 150 },
      "soulformTicked": true,
      "entitiesTicked": true,
      "sourceLogicTicked": true,
      "newVersion": 6
    },
    "version": 6
  }
}
```

### `GET /api/v1/worlds/:worldId/live` — viewer

**200 response**

```json
{
  "status": "success",
  "data": {
    "live": {
      "worldName": "SmokeWorld",
      "environmentType": "Forest",
      "dayCount": 4,
      "season": "Spring",
      "weather": "Rain",
      "livability": 62,
      "population": 4,
      "soulform": {
        "name": "SmokeSoul", "mood": "Content", "currentActivity": "Observing",
        "currentThought": "Taking in the surroundings.",
        "health": 100, "comfort": 50, "hunger": 50, "curiosity": 50
      },
      "recentEvents": [
        { "eventType": "day_advance", "description": "Day 4 began", "timestamp": "2026-10-04T09:00:00.000Z" }
      ],
      "apBalance": 150
    }
  }
}
```

### `POST /api/v1/worlds/:worldId/interventions` — member, 60/min

Spend AP on an intervention (default cost **30**).

**Request**

```json
{ "interventionType": "weather_clear", "apCost": 30 }
```

**200 response**

```json
{
  "status": "success",
  "data": { "newBalance": 120, "interventionApplied": true }
}
```

### `GET /api/v1/worlds/:worldId/ap` — viewer

AP accrual uses server time (`lastPassiveAPTime`), never client clocks.

**200 response**

```json
{
  "status": "success",
  "data": { "ap": { "balance": 150, "lastPassiveAPTime": "2026-10-04T09:00:00.000Z" } }
}
```

**C#**

```csharp
yield return BubbleApi.Send("GET", $"/api/v1/worlds/{worldId}/ap", null, jwt, null,
    json => {
        var e = JsonUtility.FromJson<Envelope<ApEnvelope>>(json);
        ArchitectPoints.SetServerBalance(e.data.ap.balance);
    }, (c, e) => Debug.LogWarning(e));
[Serializable] class ApEnvelope { public ApData ap; }
[Serializable] class ApData { public int balance; public string lastPassiveAPTime; }
```

---

## 10. Entities, bubbles, tiles

Mounted on `/api/v1/worlds` — full paths are `/api/v1/worlds/:worldId/...`.

### `GET /api/v1/worlds/:worldId/entities?type=NPC` — viewer

`type` optional: `NPC | Animal | Soulform`.

**200 response**

```json
{
  "status": "success",
  "data": {
    "entities": [
      {
        "id": "…uuid",
        "entityType": "Animal",
        "gridX": 10, "gridY": 0, "gridZ": 10,
        "facing": "North",
        "health": 100, "hunger": 30,
        "pendingMovement": "Wander",
        "targetX": null, "targetY": null, "targetZ": null,
        "targetEntityId": null,
        "isDead": false,
        "animalState": {
          "species": "Deer", "diet": "herbivore", "hungerLevel": 30,
          "maxHunger": 100, "hungerRate": 2, "currentState": "Grazing",
          "isAttacked": false, "socialCooldown": 0, "currentFoodTarget": null
        }
      }
    ]
  }
}
```

### `GET /api/v1/worlds/:worldId/entities/:entityId` — viewer

**200 response** — `{ "status": "success", "data": { "entity": { …EntityData… } } }`.

### `POST /api/v1/worlds/:worldId/entities` — member

**Request**

```json
{
  "entityType": "Animal",
  "gridX": 10, "gridY": 0, "gridZ": 10,
  "facing": "North",
  "health": 100,
  "hunger": 0,
  "animalState": { "species": "Deer", "diet": "herbivore" }
}
```

`entityType` must be `NPC`, `Animal`, or `Soulform` (checked inline → 400
otherwise). Position/facing/health/hunger and the matching `*State` blob are
optional.

**201 response** — `{ "data": { "entity": { … } } }`.

### `PATCH /api/v1/worlds/:worldId/entities/:entityId` — member

Partial update; accepted fields: `gridX/Y/Z`, `facing`,
`pendingMovement` (`None|Wander|MoveTo|Flee`), `targetX/Y/Z` (nullable),
`targetEntityId` (nullable), `isDead`, `health`, `hunger`,
`npcState` / `animalState` / `soulformEntityState`.

**200 response** — `{ "data": { "entity": { …updated… } } }`.

**C#**

```csharp
string body = JsonUtility.ToJson(new MoveReq { gridX = 11, gridZ = 9 });
yield return BubbleApi.Send("PATCH", $"/api/v1/worlds/{worldId}/entities/{entityId}", body, jwt, null,
    json => Debug.Log("moved"), (c, e) => Debug.LogError(e));
[Serializable] class MoveReq { public int gridX; public int gridZ; }
```

### `DELETE /api/v1/worlds/:worldId/entities/:entityId` — owner

**200 response**

```json
{ "status": "success", "message": "Entity removed" }
```

### `GET /api/v1/worlds/:worldId/bubbles` — viewer

```json
{
  "status": "success",
  "data": {
    "bubbles": [
      {
        "bubbleId": "main",
        "environmentType": "MeadowGrassland",
        "gridWidth": 60, "gridHeight": 60, "gridDepth": 8,
        "seasonOffset": 0, "isUnlocked": true,
        "connections": [ { "fromBubbleId": "main", "toBubbleId": "cave", "direction": "East", "isOpen": false } ]
      }
    ]
  }
}
```

### `POST /api/v1/worlds/:worldId/bubbles` — owner

**Request**

```json
{ "bubbleId": "cave", "environmentType": "Forest", "gridWidth": 40, "gridHeight": 40, "gridDepth": 6, "seasonOffset": 1 }
```

Defaults: `environmentType=MeadowGrassland`, `60×60×8`, `seasonOffset=0`.
`bubbleId` required (400 otherwise).

**201 response** — `{ "data": { "bubble": { …BubbleData… } } }`.

### `GET /api/v1/worlds/:worldId/tiles/:bubbleId` — viewer

Cells come back as a dictionary keyed by `"x,y,z"`:

```json
{
  "status": "success",
  "data": {
    "cells": {
      "4,0,7": { "x": 4, "y": 0, "z": 7, "terrain": "Grass", "elevated": false }
    }
  }
}
```

### `PUT /api/v1/worlds/:worldId/tiles/:bubbleId/:x/:y/:z` — member

Body: the cell object (shape owned by the client's `BubbleCell`).

```http
PUT /api/v1/worlds/<worldId>/tiles/main/4/0/7
Content-Type: application/json

{ "terrain": "Grass", "elevated": false, "structure": null }
```

**200 response**

```json
{ "status": "success", "message": "Tile updated" }
```

### `DELETE /api/v1/worlds/:worldId/tiles/:bubbleId/:x/:y/:z` — member

**200 response**

```json
{ "status": "success", "removed": true }
```

---

## 11. Pairing

Two sides of the same flow:

- **Device side (Unity Switch client, no login):** `POST|DELETE /pairing/device/session` with `x-api-key` + `worldKey`. The Unity app renders the returned `deepLink` as a QR code (`PairingScreenUI`).
- **Companion side (mobile app / account, JWT):** scans the QR (or types the short code) and calls `POST /pairing/claim`, then reads `GET /pairing/linked`.

Guarantees: **single-use** claim, **5-minute expiry** (`PAIRING_CODE_EXPIRY_MINUTES`), 10 pairing operations/hour.

### `POST /api/v1/pairing/session` — Bearer, pairingLimiter

JWT-side session creation for a world you own/are member of.

**Request**

```json
{ "worldId": "…uuid" }
```

**201 response**

```json
{
  "status": "success",
  "data": {
    "sessionId": "…uuid",
    "shortCode": "H7K2MQ",
    "deepLink": "bubbleworld://pair?code=H7K2MQ",
    "expiresAt": "2026-10-04T09:27:00.000Z"
  }
}
```

### `POST /api/v1/pairing/claim` — Bearer, pairingLimiter

Claims a pending session by short code (from a QR scan or manual entry).

**Request**

```json
{ "shortCode": "H7K2MQ" }
```

**200 response**

```json
{
  "status": "success",
  "data": {
    "success": true,
    "worldId": null,
    "worldKey": "default",
    "role": "viewer"
  }
}
```

- For device sessions `worldKey` carries the link (that's what the companion
  app stores to call `/sync/state` later); `worldId` is `null`.
- For JWT-side sessions `worldId` is set and a `world_members` row is created.

Errors: `404` invalid/already claimed/cancelled, `410` expired, `400` claiming
your own session.

### `DELETE /api/v1/pairing/session/:id` — Bearer

```json
{ "status": "success", "message": "Pairing session cancelled" }
```

### `POST /api/v1/pairing/device/session` — x-api-key, pairingLimiter

**Unity entry point** (`PairingService.CreateSession`). Creates a 5-minute
single-use session for the given world key.

**Request**

```json
{ "worldKey": "default" }
```

`worldKey` optional (`default`), pattern `^[A-Za-z0-9_-]{1,64}$`.

**201 response**

```json
{
  "status": "success",
  "data": {
    "sessionId": "4f9c2f1e-…uuid",
    "shortCode": "QX8N2P",
    "deepLink": "bubbleworld://pair?code=QX8N2P",
    "expiresAt": "2026-10-04T09:30:11.000Z"
  }
}
```

Errors: `401` missing/wrong `x-api-key`.

**C# — real usage from `PairingService.cs`**

```csharp
public const string DeviceSessionPath = "/api/v1/pairing/device/session";

string body = JsonUtility.ToJson(new DeviceSessionRequest { worldKey = WorldKey }); // "default"
using (var request = new UnityWebRequest(BackendGateway.ResolvedBase + DeviceSessionPath, "POST"))
{
    request.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(body));
    request.downloadHandler = new DownloadHandlerBuffer();
    request.SetRequestHeader("Content-Type", "application/json");
    request.SetRequestHeader("x-api-key", BackendGateway.ApiKey);
    request.timeout = BackendGateway.RequestTimeoutSeconds;
    yield return request.SendWebRequest();

    if (request.result != UnityWebRequest.Result.Success) { /* Describe() → 401/5xx/connection */ yield break; }

    var envelope = JsonUtility.FromJson<DeviceSessionEnvelope>(request.downloadHandler.text);
    // envelope.data: sessionId, shortCode, deepLink, expiresAt
    PairingSession session = BuildSession(envelope.data);   // parses expiry, builds QR texture
}

[Serializable] class DeviceSessionRequest { public string worldKey; }
[Serializable] class DeviceSessionEnvelope { public DeviceSessionData data; }
[Serializable] class DeviceSessionData {
    public string sessionId; public string shortCode; public string deepLink; public string expiresAt;
}
```

The QR texture is rendered from `deepLink` with `PairingService.BuildQrTexture`
(pure-C# `QrCodeEncoder`, byte mode, EC-M).

### `DELETE /api/v1/pairing/device/session/:id?worldKey=default` — x-api-key

Best-effort cancel when the overlay closes (the code stops being claimable
immediately).

```json
{ "status": "success", "message": "Pairing session cancelled" }
```

Errors: `400` malformed id, `404` not found **or** wrong `worldKey`.

**C#**

```csharp
string url = BackendGateway.ResolvedBase + DeviceSessionPath + "/" + sessionId +
             "?worldKey=" + UnityWebRequest.EscapeURL(WorldKey);
using (var request = UnityWebRequest.Delete(url))
{
    request.SetRequestHeader("x-api-key", BackendGateway.ApiKey);
    request.timeout = BackendGateway.RequestTimeoutSeconds;
    yield return request.SendWebRequest();   // best-effort: ignore result
}
```

### `GET /api/v1/pairing/linked` — Bearer

World keys the account has linked by claiming device sessions.

```json
{ "status": "success", "data": { "worldKeys": ["default"] } }
```

---

## 12. Companion

### `POST /api/v1/companion/:worldId/chat` — member, 30/min

Player message → brain reply. **Charges AP by length**: 1 AP ≤20 words,
2 AP ≤50 words, else 3 AP. Persists both messages (new session if `sessionId`
omitted).

**Request**

```json
{ "text": "How are you doing?", "sessionId": "…uuid (optional)" }
```

**201 response**

```json
{
  "status": "success",
  "data": {
    "playerMessage": {
      "id": "…uuid", "speaker": "Player", "text": "How are you doing?",
      "apCost": 1, "createdAt": "2026-10-04T09:31:00.000Z"
    },
    "soulformMessage": {
      "id": "…uuid", "speaker": "Soulform",
      "text": "The rain makes the meadow sing — I was just listening.",
      "emotion": "Content", "expression": "Happy", "animation": "Nod",
      "apCost": 0, "createdAt": "2026-10-04T09:31:02.000Z"
    },
    "brainResponse": { "…BrainResponse (see §14)…": "…" }
  }
}
```

### `GET /api/v1/companion/:worldId/chat?sessionId=&limit=50&offset=0` — viewer

Without `sessionId`, messages come from the most recent session.

```json
{
  "status": "success",
  "data": {
    "sessions": [
      { "id": "…uuid", "title": "How are you doing?", "hasUnread": false, "updatedAt": "2026-10-04T09:31:00.000Z" }
    ],
    "messages": [
      { "id": "…", "speaker": "Player", "text": "How are you doing?", "apCost": 1, "createdAt": "…" },
      { "id": "…", "speaker": "Soulform", "text": "…", "emotion": "Content", "expression": "Happy", "animation": "Nod", "createdAt": "…" }
    ]
  }
}
```

### `GET /api/v1/companion/:worldId/memories` — viewer

```json
{
  "status": "success",
  "data": {
    "chapters": [
      {
        "id": "…uuid",
        "chapterIndex": 0,
        "summary": "The world began quiet and green.",
        "startTime": "2026-10-01T10:00:00.000Z",
        "endTime": null,
        "importantPeople": [], "importantPlaces": [], "skillsLearned": [], "majorEvents": [],
        "entries": [
          { "id": "…", "description": "First rain of spring", "category": "weather", "keywords": ["rain"], "createdAt": "…" }
        ]
      }
    ]
  }
}
```

**C#**

```csharp
[Serializable] class ChatReq { public string text; public string sessionId; }
var req = new ChatReq { text = "How are you doing?" };   // sessionId null → new session
string body = JsonUtility.ToJson(req);
yield return BubbleApi.Send("POST", $"/api/v1/companion/{worldId}/chat", body, jwt, null,
    json => {
        var e = JsonUtility.FromJson<Envelope<ChatResult>>(json);
        AddBubble(e.data.playerMessage.text, isPlayer: true);
        AddBubble(e.data.soulformMessage.text, isPlayer: false);
    },
    (c, err) => ShowToast(c == 429 ? "Too many messages — slow down." : err));
[Serializable] class ChatResult { public ChatMsg playerMessage; public ChatMsg soulformMessage; }
[Serializable] class ChatMsg { public string id; public string speaker; public string text;
                              public string emotion; public string expression; public string animation; }
```

---

## 13. Game data

JWT routes with inline owner/member checks (not the `requireWorldAccess`
middleware).

### `POST /api/v1/game-data/:worldId/soulform/brain` — member

Same brain as `/ai/chat` (§14) but wrapped in the standard envelope and
persisting the exchange.

**Request**

```json
{ "mode": "chat", "playerMessage": "Hello there!" }
```

`mode`: `chat` (default, requires `playerMessage`) or `tick`
(`playerMessage` omitted → brain tick). Viewers get `403` in chat mode.

**200 response**

```json
{
  "status": "success",
  "data": {
    "brainResponse": {
      "currentThought": "Someone said hello — I should wave back.",
      "mood": "Content",
      "currentActivity": "Greeting",
      "targetX": null, "targetY": null, "targetZ": null,
      "expression": "Happy", "animation": "Wave", "emotionTag": "joy",
      "action": "SpeakToPlayer", "actionTarget": null
    },
    "attempts": 1,
    "safetyStatus": "ok"
  }
}
```

### `GET /api/v1/game-data/:worldId/soulform` — viewer†

Raw `soulform_states` row (**snake_case**):

```json
{
  "status": "success",
  "data": {
    "soulform": {
      "world_id": "…uuid",
      "name": "SmokeSoul", "meaning": "…", "nature": "Gentle",
      "temperament": "Curious", "mood": "Content",
      "grid_x": 5, "grid_y": 0, "grid_z": 5,
      "comfort": 50, "hunger": 40, "curiosity": 60, "health": 100,
      "current_activity": "Observing", "current_thought": "Taking in the surroundings.",
      "current_expression": "Neutral", "current_animation": "Idle",
      "appearance": "{}", "voice": "{}", "outfit": "{}",
      "bond_traits": "{}", "skill_data": "{}"
    }
  }
}
```

### `PATCH /api/v1/game-data/:worldId/soulform` — **owner only**

Whitelisted fields only:
`name, meaning, nature, temperament, mood, grid_x/y/z, comfort, hunger,
curiosity, health, current_activity, current_thought, current_expression,
current_animation, appearance, voice, outfit, bond_traits, skill_data`.

**Request**

```json
{ "mood": "Excited", "current_activity": "Building", "hunger": 35 }
```

**200 response** — updated full `soulform` row (as `GET` above).

### `GET /api/v1/game-data/:worldId/events?limit=50&offset=0` — viewer†

Newest first; rows are **snake_case**:

```json
{
  "status": "success",
  "data": {
    "events": [
      {
        "id": "…uuid",
        "event_type": "day_advance",
        "description": "Day 4 began with light rain.",
        "source": "simulation",
        "event_data": { "day": 4 },
        "server_timestamp": "2026-10-04T09:00:00.000Z"
      }
    ]
  }
}
```

### `POST /api/v1/game-data/:worldId/events` — **owner only**

**Request**

```json
{
  "eventType": "memory_recorded",
  "description": "Soulform learned to weave reeds.",
  "source": "client",
  "eventData": { "skill": "weaving" }
}
```

**201 response**

```json
{
  "status": "success",
  "data": { "event": { "id": "…uuid", "server_timestamp": "2026-10-04T09:33:00.000Z" } }
}
```

---

## 14. AI (Soulform brain)

The backend owns prompt building, Bedrock credentials (SigV4), retries, and
hallucination gates — the client ships only the world-context string.

### `POST /api/v1/ai/chat` — x-api-key, 30/min — **flat response**

**Request**

```json
{
  "worldContext": "Name: Lyra\nTemperament: Curious\nWeather: Rain\nFood: 62\nNearby: Deer, NPC\nHunger: 40\nMemories: Day 1 — first rain\n",
  "message": "Hello there!"
}
```

| Field | Rules |
|---|---|
| `worldContext` | 1–20000 chars, required (the client-built context block) |
| `message` | 1–2000 chars; **omit for a brain tick** ("Tick. Decide what to do.") |

**200 response — flat, no envelope (Unity parses this directly):**

```json
{
  "currentThought": "Someone said hello — I should wave back.",
  "mood": "Content",
  "currentActivity": "Greeting",
  "targetX": null,
  "targetY": null,
  "targetZ": null,
  "expression": "Happy",
  "animation": "Wave",
  "emotionTag": "joy",
  "action": "SpeakToPlayer",
  "actionTarget": null,
  "model": "us.meta.llama3-1-8b-instruct-v1:0",
  "attempts": 1,
  "safetyStatus": "ok"
}
```

Field notes:

- `targetX/Y/Z` are **JSON strings or null** (Unity `JsonUtility` deserializes
  them into string fields and does not coerce numbers) — e.g. `"7"` or `null`.
- `model: "mock"` means Bedrock was unreachable/unconfigured and a canned
  response was served (the game keeps running).
- `attempts` = retries used; `safetyStatus` ∈ `ok | corrected`.
- `expression` ∈ `Neutral, Happy, Sad, Surprised, Curious, Thinking, Excited, Tired, Distressed`;
  `animation` ∈ `Idle, Wave, Nod, ShakeHead, LookAround, Point, Sit, Walk, Celebrate, Defeated`;
  `action` ∈ `MoveToLocation, GoToShelter, SearchForFood, CollectResource, BuildObject, Rest, ObserveObject, SpeakToPlayer, SpeakToSoulform, FleeDanger, PlayAnimation, ContinueCurrentTask`.

**C# — real usage from `SoulformBrainService.QueryBackendBrain`**

```csharp
yield return BackendGateway.EnsureResolved();
string endpoint = BackendGateway.ResolvedBase != null
    ? BackendGateway.ResolvedBase + BackendGateway.BrainPath   // "/api/v1/ai/chat"
    : null;
if (endpoint == null) { /* serve mock response, keep game running */ yield break; }

string jsonPayload = "{\"worldContext\":" + JsonUtility.ToJson(worldContext) +
    (string.IsNullOrEmpty(playerMessage) ? "" : ",\"message\":" + JsonUtility.ToJson(playerMessage)) + "}";

using (var request = new UnityWebRequest(endpoint, "POST"))
{
    request.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(jsonPayload));
    request.downloadHandler = new DownloadHandlerBuffer();
    request.SetRequestHeader("Content-Type", "application/json");
    request.SetRequestHeader("x-api-key", config.ApiKey);   // SoulformBrainConfig.ApiKey
    request.timeout = config.TimeoutSeconds;
    yield return request.SendWebRequest();

    if (request.result == UnityWebRequest.Result.Success)
    {
        SoulformBrainResponse response = ParseResponse(request.downloadHandler.text); // flat JSON
        if (response != null && ValidResponse(response))
        {
            string failure = RunHallucinationChecks(response, worldContext);
            if (failure == null) { FinalizeBrainResponse(response, playerMessage, onResult); yield break; }
            // failure → retry with rewrite note, else ApplySelfCorrection(...)
        }
    }
    else if (request.result == UnityWebRequest.Result.ConnectionError)
    {
        BackendGateway.Invalidate();   // next attempt re-probes local ↔ remote
    }
}
```

Errors: `401` bad key · `400` validation (context too long / empty message) ·
`429` chat rate limit · `5xx` Bedrock failure after retries.

### `GET /api/v1/ai/status` — public

Runtime config probe (`BackendGateway.StatusPath`) — the backend is the single
source of truth for tick cadence and feature flags; the ScriptableObject only
holds connection links.

**200 response**

```json
{
  "status": "success",
  "data": {
    "bedrockConfigured": true,
    "model": "us.meta.llama3-1-8b-instruct-v1:0",
    "region": "us-east-1",
    "brain": {
      "tickSeconds": 5,
      "maxRetries": 2,
      "tickMaxRetries": 1,
      "maxGenLen": 250,
      "apiKeyRequired": true
    },
    "features": { "chat": true, "tick": true, "worldSync": true }
  }
}
```

**C# — real usage from `BackendGateway.cs`**

```csharp
public const string StatusPath = "/api/v1/ai/status";

foreach (string candidate in Candidates())          // local link first, then remote
{
    using (var request = UnityWebRequest.Get(candidate + StatusPath))
    {
        request.timeout = 2;
        yield return request.SendWebRequest();
        if (request.result != UnityWebRequest.Result.Success) continue;

        ResolvedBase = candidate;                   // first 200 wins, cached for session
        ApplyStatusConfig(request.downloadHandler.text);
        // → ServerTickSeconds (data.brain.tickSeconds), WorldSyncEnabled (data.features.worldSync)
        yield break;
    }
}
```

---

## 15. Sync (world snapshot)

Opaque-string snapshot store: **the client owns the state shape**
(`BubbleWorldSaveData` JSON), the backend owns versioning and durability.
This is the canonical save — the Switch local save and Nintendo Cloud Save are
caches only.

### `GET /api/v1/sync/state?worldKey=default` — x-api-key

**200 response — nothing stored yet**

```json
{ "status": "success", "data": { "worldKey": "default", "version": 0, "state": null, "updatedAt": null } }
```

**200 response — snapshot present**

```json
{
  "status": "success",
  "data": {
    "worldKey": "default",
    "version": 7,
    "state": "{\"saveVersion\":\"2.1\",\"worldName\":\"Bubble World\", … }",
    "updatedAt": "2026-10-04T09:35:00.000Z"
  }
}
```

`state` is a **JSON string** (not an object). `worldKey` must match
`^[A-Za-z0-9_-]{1,64}$` (400 otherwise), default `default`.

### `PUT /api/v1/sync/state` — x-api-key

**Request**

```json
{
  "worldKey": "default",
  "state": "{\"saveVersion\":\"2.1\",\"worldName\":\"Bubble World\", …}"
}
```

| Field | Rules | Default |
|---|---|---|
| `worldKey` | `^[A-Za-z0-9_-]{1,64}$` | `default` |
| `state` | 1–20,000,000 chars, must parse as valid JSON | — |

Invalid JSON → `400 {"status":"error","message":"state must be valid JSON"}`.

**200 response** — same shape as GET with the incremented version:

```json
{ "status": "success", "data": { "worldKey": "default", "version": 8, "state": "…", "updatedAt": "2026-10-04T09:36:10.000Z" } }
```

**C# — real usage from `ServerSyncService.cs`**

```csharp
// Pull (startup — backend is the authoritative save)
string url = BackendGateway.ResolvedBase + BackendGateway.SyncPath +   // "/api/v1/sync/state"
             "?worldKey=" + DefaultWorldKey;                           // "default"
using (var request = UnityWebRequest.Get(url))
{
    request.SetRequestHeader("x-api-key", BackendGateway.ApiKey);
    request.timeout = BackendGateway.RequestTimeoutSeconds;
    yield return request.SendWebRequest();

    if (request.result == UnityWebRequest.Result.Success)
    {
        var envelope = JsonUtility.FromJson<SyncEnvelope>(request.downloadHandler.text);
        if (envelope?.data != null && !string.IsNullOrEmpty(envelope.data.state))
            ApplyAuthoritativeSnapshot(envelope.data.state, envelope.data.version);
        // state == null → backend reachable, nothing stored: keep local save
    }
    else { lastStatus = ServerStatus.Offline; }
}

// Push (after every local save)
string body = JsonUtility.ToJson(new SyncPutRequest { worldKey = DefaultWorldKey, state = saveJson });
using (var request = new UnityWebRequest(BackendGateway.ResolvedBase + BackendGateway.SyncPath, "PUT"))
{
    request.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(body));
    request.downloadHandler = new DownloadHandlerBuffer();
    request.SetRequestHeader("Content-Type", "application/json");
    request.SetRequestHeader("x-api-key", BackendGateway.ApiKey);
    request.timeout = BackendGateway.RequestTimeoutSeconds;
    yield return request.SendWebRequest();
    bool stored = request.result == UnityWebRequest.Result.Success;   // version incremented
}

[Serializable] class SyncEnvelope { public SyncData data; }
[Serializable] class SyncData { public string worldKey; public int version; public string state; public string updatedAt; }
[Serializable] class SyncPutRequest { public string worldKey; public string state; }
```

---

## 16. Appendix

### A. End-to-end flows

**1. Unity brain turn (no login)**

```
Unity                         Backend                        Bedrock
  │ POST /ai/chat (x-api-key)   │                              │
  │  {worldContext, message}    │  build prompt + gates        │
  │ ───────────────────────────▶│ ────────────────────────────▶│
  │                             │◀── model output (retries) ───│
  │◀── flat game JSON ──────────│                              │
  │ ParseResponse + hallucination checks (client side)         │
```

**2. QR pairing (Switch → phone)**

```
Switch (PairingService)              Backend                    Phone (companion app)
  │ POST /pairing/device/session      │
  │  {worldKey:"default"}             │
  │ ─────────────────────────────────▶│  creates pending, 5-min expiry
  │◀── sessionId, shortCode, deepLink │
  │ render QR (deepLink)              │
  │                     scan QR ──────┼────────────────────────▶ │
  │                                   │◀── POST /pairing/claim {shortCode} (JWT)
  │                                   │──▶ {worldKey:"default", role:"viewer"}
  │ close overlay → DELETE            │◀── GET /pairing/linked (JWT)
  │  /pairing/device/session/:id      │──▶ {worldKeys:["default"]}
```

Single-use: the second claim of the same code returns `404`.

**3. Save cycle**

```
Unity saves → PUT /sync/state (x-api-key, full JSON string) → version++
Unity boots → GET /sync/state       → authoritative snapshot (server wins)
JWT clients → GET /worlds/:id/state → same data via the account path
```

### B. Response-shape quick reference

| Endpoint | Wrapper | Key naming |
|---|---|---|
| `GET /health` | none | camel |
| `POST /ai/chat` | **none (flat)** | camel |
| `GET /worlds` | `data.worlds[]` | **snake** |
| `POST /worlds` | `data.world` | **snake** |
| `GET /worlds/:id/state` | `data.state` | camel |
| `GET /worlds/:id/live` | `data.live` | camel |
| `GET .../soulform` | `data.soulform` | **snake** |
| `GET .../events` | `data.events[]` | **snake** |
| `POST .../soulform/brain` | `data.brainResponse` | camel |
| `GET /sync/state` | `data` (worldKey/version/state/updatedAt) | camel |
| pairing create (both sides) | `data` (sessionId/shortCode/deepLink/expiresAt) | camel |
| `POST /pairing/claim` | `data` (success/worldId/worldKey/role) | camel |

### C. Status code map

| Code | Meaning in this API |
|---|---|
| 200 | OK |
| 201 | Created (register, create world/entity/bubble, pairing session, event, companion chat) |
| 400 | Zod validation failure (has `details`), bad coords, bad worldKey/state |
| 401 | Missing/invalid JWT or `x-api-key` |
| 403 | Authenticated but wrong world role (or soulform PATCH by non-owner) |
| 404 | Unknown world/entity, invalid or already-claimed pairing code, wrong-world cancel |
| 410 | Pairing code expired |
| 429 | Rate limited (`RateLimit-*` headers present) |
| 500 | Unhandled server error (`{"status":"error","message":"Internal server error"}`) |

### D. Verified test suites

| Script | Coverage |
|---|---|
| `smoke-test.ps1` | 14 checks: health, register, world CRUD, state PUT/GET, entities, tick, brain, companion chat, cross-player 403, unauthenticated 401 |
| `test-device-pairing.ps1` | 11 checks: device session create, missing/wrong key 401, claim → worldKey, single-use 404, linked list, cancel, double-cancel 404, cancelled code unclaimable, wrong-worldKey 404 |
