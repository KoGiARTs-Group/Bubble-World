# Bubble World Implementation Plan

# Every world begins with imagination J.A.Montemoiño II

---

## Current State Summary

The codebase is at **Milestone 2 completion** with solid foundations:

- Tile system (composition, auto-tiling, water flow) — Complete
- Source logic (water/sun/earth cycle, tile conversion, plant growth) — Complete
- Weather (seasonal probability tables) — Complete
- Skills (28 skill sets, star progression, sustainability ratings) — Complete
- Memory (chapters, context window, long-term bank) — Complete
- AP Economy (interventions, communication, passive accrual) — Complete
- AI Brain (mock/OpenAI/AWS Bedrock) — Complete
- Animal AI (diet-based behavior) — Complete
- NPC AI (3-phase evaluation) — Complete
- Save system (v2.1 JSON) — Complete

### Key Gaps

1. **No entity instances** — AI classes exist but no NPC/Animal registry, position tracking, or visual spawning
2. **NPCAI not wired** — `OnGameTick()`/`OnFastForward()` are empty; `EvaluateDaily()` must be called externally
3. **BubbleStarterFactory** — All 3 environments generate identical terrain
4. **No backend-primary save/sync** — Only AI brain has network code; no canonical backend data layer
5. **No animal/NPC visual representation** in the world
6. **No Soulform-to-Unity action pipeline** — Action validation exists but no execution/animation

---

## Technical Decision: Backend-Primary Player Data

**Bubble World backend is the primary save system.** Nintendo Switch local save and Nintendo Cloud Save are cache/recovery layers only.

- Each player has their own backend account, player data, stats, world state, Soulform state, AP ledger, memory, chat history, and live-mode state.
- The backend must never send every player the same canonical game data. Shared/static config may be global, but world state is scoped by authenticated player and authorized world membership.
- Nintendo local save stores only last-known authorized snapshot, pending offline action journal, last sync checkpoint, player settings, and recovery metadata.
- Nintendo Cloud Save, if used, backs up the Switch-side cache/recovery data only. It is not the data source for companion mobile chat or live mode.
- The companion mobile app reads and writes through Bubble World backend APIs after login or QR/pairing-code linking; it does not read Nintendo save files or Nintendo Cloud Save.
- Offline Switch play is allowed through a local action journal. On reconnect, the backend validates and reconciles queued actions before updating the canonical state.

---

## Architecture Principle: Server-Authoritative Abstraction

Since Bubble World needs Nintendo Switch play plus companion mobile chat/live mode, **every simulation system must be built with a data-provider interface** so the backend can own canonical player/world state while Unity clients render, cache, and submit actions.

```
┌─────────────────────────────────────────────────┐
│                    UNITY CLIENT                  │
│  ┌───────────┐  ┌───────────┐  ┌──────────────┐ │
│  │ Rendering │  │    UI     │  │  Audio/Anim   │ │
│  └─────┬─────┘  └─────┬─────┘  └──────┬───────┘ │
│        │              │               │           │
│  ┌─────▼──────────────▼───────────────▼───────┐  │
│  │          GAME STATE MANAGER               │  │
│  │   (single source of truth for all data)   │  │
│  └──────────────────┬────────────────────────┘  │
│                     │                            │
│  ┌──────────────────▼────────────────────────┐  │
│  │      IWorldDataProvider (interface)        │  │
│  │  ┌─────────────────┐ ┌──────────────────┐ │  │
│  │  │ LocalSimulation │ │ ServerProvider   │ │  │
│  │  │ (offline cache)  │ │ (HTTP canonical) │ │  │
│  │  └─────────────────┘ └──────────────────┘ │  │
│  └───────────────────────────────────────────┘  │
└─────────────────────────────────────────────────┘
```

The default production path is `ServerWorldDataProvider`. `LocalWorldDataProvider` exists for offline cache, development, and recovery, but it is not the canonical save source.

---

## Phase 0: Server-Ready Data Layer (Foundation)

**Goal:** Introduce the abstraction layer that makes all future phases compatible with backend-primary saves, Nintendo cache/recovery, and companion mobile access.

### 0.1 — `IWorldDataProvider` Interface

Create a single interface that all simulation systems call instead of directly reading/writing `BubbleModel`, `TileState`, etc.

```
IWorldDataProvider
  ├── GetTileComposition(x,y,z) → TileComposition
  ├── SetTileComposition(x,y,z, comp)
  ├── GetTileState(x,y,z) → TileState
  ├── SetTileState(x,y,z, state)
  ├── GetWeather() → WeatherType
  ├── SetWeather(weather)
  ├── GetSeason() → SeasonType
  ├── GetEntity(id) → EntityData
  ├── GetAllEntities() → List<EntityData>
  ├── GetSoulform() → SoulformModel
  ├── GetAP() → ArchitectPoints
  ├── GetPlayerId() → string
  ├── GetWorldId() → string
  ├── PushAction(action) → ValidationResult
  └── SyncFromServer(data)  // receive authorized player/world state
```

### 0.2 — `LocalWorldDataProvider` Implementation

Wraps existing systems (`BubbleModel`, `WeatherManager`, `SourceLogicSystem`, etc.) behind the interface. All existing code gradually migrates to call the interface.

### 0.3 — `GameStateManager` (Singleton)

- Holds the current `IWorldDataProvider`
- Broadcasts state-change events for rendering/UI
- On startup: authenticates player → if online, use `ServerWorldDataProvider`; if offline, use `LocalWorldDataProvider` with cached snapshot and pending journal
- On server sync: receives diff, applies to local state, triggers visual refresh

### 0.4 — `ServerSyncService`

- `CheckForServerUpdate()` — reports online/offline/backend version
- `PullLatestState()` — pulls authorized player/world snapshot or diff
- `PushLocalChanges(changes)` — uploads pending action journal when online
- `GetServerStatus()` — returns `Online`, `Offline`, `AuthRequired`, or `SyncConflict`
- Full HTTP implementation lives in Phase 8 and `Assets/Scripts/BubbleWorld/Plans/backend-server-implementation-plan.md`

---

## Phase 1: Entity Management System

**Goal:** NPCs, Animals, and the Soulform exist as real entities with positions, states, and visual representation in the world.

### 1.1 — Entity Data Models

```
EntityData (base)
  ├── EntityId (string GUID)
  ├── EntityType (NPC / Animal / Soulform)
  ├── GridX, GridY, GridZ
  ├── Facing direction
  ├── CurrentState (enum)
  ├── Hunger (0-100)
  ├── Health (0-100)

NPCData : EntityData
  ├── BaselineState (Busy/Socializing/Working)
  ├── CurrentState (7 states from GDD)
  ├── IsDisgruntled (bool)
  ├── DisgruntledDays (int)
  ├── ResourceAdequacy tracking
  ├── Shelter status

AnimalData : EntityData
  ├── Species (Wolf/Sheep/Bear/etc.)
  ├── Diet (Carnivore/Herbivore/Omnivore)
  ├── HungerLevel (0-100)
  ├── AnimalState (7 states)

SoulformEntityData : EntityData
  ├── (wraps existing SoulformModel)
  ├── CurrentSkillAttempt
  ├── CurrentActivity
```

### 1.2 — `EntityManager`

- `Register(entity)` / `Deregister(id)` — add/remove entities
- `GetEntity(id)` / `GetEntitiesAt(x,y,z)` / `GetEntitiesInRange(center, radius)`
- `GetAllNPCs()` / `GetAllAnimals()` / `GetSoulform()`
- `MoveEntity(id, newX, newY, newZ)` — validates movement, updates grid
- Event: `OnEntitySpawned`, `OnEntityRemoved`, `OnEntityMoved`, `OnEntityStateChanged`

### 1.3 — `EntityVisualManager`

- Spawns/destroys GameObjects for each entity
- Updates position/animation based on EntityManager events
- Uses a simple entity prefab pool (placeholder models for vertical slice)
- Soulform uses existing `BubbleWorldGrid.SpawnSoulformVisual()`

### 1.4 — Wire `SpawnManager` → `EntityManager`

- When `SpawnManager` fires `OnNPCBorn` → `EntityManager.Register(npcData)` → `EntityVisualManager` spawns visual
- Same for `OnAnimalSpawned`
- Disgruntled tracking: `SpawnManager.TrackResourceDay()` already exists; wire its output to `NPCData.IsDisgruntled`

---

## Phase 2: NPC & Animal Simulation Integration

**Goal:** Wire the existing AI classes into the game tick loop so entities actually behave.

### 2.1 — Wire NPCAI into GameTimeManager

- `GameTimeManager` already dispatches `OnGameTick` to `IWorldTimeReceiver` instances
- Register `NPCAI` as a time receiver
- `NPCAI.OnGameTick()` implementation:
  - Daily: call `EvaluateDaily()` → determine action
  - Hourly: update hunger, check resource proximity
  - Per-tick: execute current action (move toward target, interact)

### 2.2 — Wire AnimalAI into GameTimeManager

- `AnimalAI` already implements `IWorldTimeReceiver`
- Register each animal instance as a receiver
- `OnGameTick()`: hunger decay, state transitions, movement decisions
- Movement: animals pathfind toward food/water/same-species using `Pathfinding3D`

### 2.3 — Entity Movement System

- `EntityMovementSystem` — processes movement queues for all entities
- Each tick: entities with pending moves advance one tile toward their destination
- Validates destination is walkable (not `IsObject`, not occupied by hostile)
- Supports: Wander (random adjacent), MoveTo (pathfind), Flee (opposite direction from threat)

### 2.4 — Animal Hunt/Forage Logic

- Carnivore: when hungry, find nearest herbivore → pathfind → attack on arrival
- Herbivore: when hungry, find nearest plant/grass tile → pathfind → eat on arrival
- Omnivore: roll per GDD (1-85% forage, 86-100% hunt) → then behave as carnivore or herbivore

### 2.5 — NPC Daily Cycle

- Morning: evaluate baseline → determine daily plan
- Through day: execute plan (forage, socialize, build, etc.)
- Night: seek shelter or sleep
- Community conversation: 50% chance to talk about major event (from `EventLog`)

### 2.6 — NPC Scarcity Response

- When `IsDisgruntled` for 3+ days → roll from `ScarcityResponse` enum (Cooperate, Fight, Migrate, Theft, Starvation, LeadershipFormation)
- Migrate: NPC moves to adjacent bubble (if connected)
- Leadership: creates a representative NPC (future: SLM leader)

---

## Phase 3: Environment Differentiation & Water Mechanics

**Goal:** Fix the starter factory, implement half-cube combining, and complete the water propagation rules.

### 3.1 — Fix `BubbleStarterFactory`

- **Meadow:** Dry dirt base, grass near water, single stream, apple tree
- **Forest:** Rich earth base, dense trees, multiple streams, mushrooms
- **Beach:** Sand base, shallow water edges, palm trees, limited fresh water
- Add the GDD's "Beginner Mode" opening: dying apple tree, dead deer, dwindling stream

### 3.2 — Half-Cube Layering

- A tile at half-height can have a second half on top (e.g., Earth base + Snow top)
- `TileComposition` gains `HalfTile` boolean flag
- Half tiles with water on top: double moisture content of connected tiles (per GDD section 2)
- `AutoTileSystem` already handles half-variants; verify height transitions

### 3.3 — Stream/River Formation Rules

- Per GDD: streams only form when tiles connect to a continuous water source
- Rivers/streams must connect from one map edge to another, or from a body of water
- Isolated excess water forms puddles (not streams)
- `SourceLogicSystem.HasConnectedWaterSource()` already exists; ensure it enforces edge-to-edge connectivity

### 3.4 — Water Propagation Distance Rules

- Stream tiles: +2 water to adjacent, +1 to 1-tile-away
- Water tiles: +3 to adjacent, +2 at 2 tiles, +1 at 3 tiles
- Plant roots: 1-tile radius
- Bush roots: 2-tile radius
- Tree roots: 3-tile radius
- Add these to `SourceLogicConfig.WaterPropagationEntry` and `PropagateWater()`

### 3.5 — Rain → Fluid Layer Activation

- Per GDD: rain increases moisture, but only activates visible Fluid Layer when tile capacity is exceeded (overflow)
- `SourceLogicSystem.AccumulateResources()` already adds water; need overflow check that sets `FluidType` on the tile

---

## Phase 4: Soulform Action Pipeline

**Goal:** The Soulform's AI decisions translate into visible in-game actions with consequences.

### 4.1 — `SoulformActionExecutor`

- Receives `SoulformActionCommand` (validated by `ActionValidator`)
- Executes the action: moves soulform, modifies tiles, consumes resources
- Reports result back to `SoulformBrainService` for next context update
- Triggers `SkillProgression.AttemptSkill()` for relevant actions

### 4.2 — Skill Attempt → Visual Feedback

- When Soulform attempts fire-making: play animation, show spark VFX
- Success: fire appears on tile, skill execution +1
- Failure: no fire, "thinking" animation, retry possible
- Five successful executions → solid star → skill acquired

### 4.3 — Conversation-to-Action Pipeline

- Per GDD topic 14: Soulform says "I'll gather dry wood and try to make a fire"
- AI sends hidden command: `Attempt Firemaking`
- Unity checks materials/conditions → allows or blocks
- Result fed back to AI: "You found dry wood near the stream" or "No suitable wood nearby"

### 4.4 — Failure Consequences (GDD topic 15)

- Wrong material: skill attempt fails, time/resources wasted
- Eating unsafe food: health damage, sickness state
- Refusing advice: Soulform acts independently (it already does — AI has autonomy)
- Dangerous decision: can lead to Soulform/NPC/animal death
- Death: `SoulformDeathHandler` — game over state for vertical slice

---

## Phase 5: Weather & Season Visual Effects

**Goal:** Weather and seasons are visible and affect gameplay.

### 5.1 — Weather Visual System

- Rain: particle system, darkened sky, rain sound
- Snow: particle system, white overlay, cold sound
- Sunny: bright lighting, warm color grading
- Storms (1/2/3): increasing intensity — lightning, thunder, wind particles, camera shake
- Windy: tree sway, grass bend, dust particles

### 5.2 — Season Visuals

- Spring: green tint, flower particles
- Summer: warm golden light, heat shimmer
- Autumn: orange/brown color grading, leaf particles
- Winter: blue-white tint, frost overlay, snow accumulation on tiles

### 5.3 — Weather → Tile Effects

- Rain: +3 water per GDD
- Snow: +2 snow per GDD (only in winter bubbles)
- Sunny: +2 sun per GDD
- Cloudy: +1 sun per GDD
- Snow can only exist in winter-season bubbles (per GDD)

### 5.4 — Storm Damage

- Storm 1: minor erosion, plant damage
- Storm 2: fire risk, shelter damage, animal/NPC flee
- Storm 3: flooding, structure destruction, population loss

---

## Phase 6: Time Progression & Offline Catch-up

**Goal:** Full time system with speed controls and offline progression.

### 6.1 — Time Speed Controls

- Per GDD topic 16: x1, x2, x4, x10 (one-time choice per session)
- UI: speed selector on Live screen
- `GameTimeManager` already has multiplier support

### 6.2 — Day/Night Cycle

- Visual: sun/moon position, ambient light changes
- Gameplay: night does not count toward "excess sun" days (per GDD)
- Animals/NPCs: reduced activity at night

### 6.3 — Offline Progression

- `GameTimeManager.ProcessOfflineCatchUp()` already exists
- Calculate: days passed → run weather rolls, source logic ticks, hunger decay, skill cooldowns
- Chunk the calculation (already implemented)
- On return: show summary of what happened ("While you were away: 2 rainy days, 1 new NPC born")

### 6.4 — Tutorial Protection

- Per GDD: 7 days of tutorial protection (already in `GameTimeConfig`)
- No storms, no starvation, no animal attacks during tutorial

---

## Phase 7: Multi-Bubble & Connections

**Goal:** Support 3 bubbles for the vertical slice with connections between them.

### 7.1 — `BubbleWorldManager`

- Manages multiple `BubbleModel` instances
- Each bubble has its own grid, weather, entities, and season offset
- Bubble connection map: which bubbles are neighbors (N/E/S/W)
- Active bubble: the one the player is currently viewing

### 7.2 — Bubble-to-Bubble Connections

- Visual: bridge/portal between connected bubbles
- Entity migration: NPCs/animals can move between connected bubbles
- Resource sharing: adjacent bubble resources count toward sustainable capacity (per GDD topic 34)
- Stream connection: water can flow between adjacent bubbles

### 7.3 — Bubble Navigation UI

- Switch between bubbles in the live view
- Show overview of all owned bubbles
- Locked/unlocked bubble states

---

## Phase 8: Backend Server Integration (Required)

**Goal:** Make Bubble World backend the canonical save system for Nintendo Switch and companion mobile. Unity clients become rendering/input/cache clients for authorized player-specific world state.

See the detailed backend implementation document:
`Assets/Scripts/BubbleWorld/Plans/backend-server-implementation-plan.md`

### 8.1 — Server Architecture

```
/api/v1
  /auth/platform/nintendo        POST  — link/refresh Nintendo platform identity where permitted
  /pairing/session               POST  — create short-lived QR/pairing session
  /pairing/claim                 POST  — mobile claims pairing code
  /players/me                    GET   — signed-in player profile and stats
  /worlds                        GET   — worlds owned/shared with signed-in player
  /worlds/:worldId/state         GET   — authorized full world snapshot
  /worlds/:worldId/sync          GET   — authorized diff since sync checkpoint
  /worlds/:worldId/actions       POST  — validated player action or offline journal batch
  /worlds/:worldId/chat          POST  — Soulform chat using that player's world state
  /worlds/:worldId/live          GET   — filtered companion live-mode summary
```

### 8.2 — Per-Player Data Isolation

- Every account has a stable `playerId`.
- Every world has an owner and optional membership list.
- Every state API authorizes by signed-in `playerId` plus `worldId`.
- Backend returns only the player's authorized worlds, stats, AP ledger, Soulform state, memory, chat, and live-mode data.
- Static game config can be shared globally, but canonical world state is never broadcast as one shared state to all players.

### 8.3 — `ServerWorldDataProvider`

- Implements `IWorldDataProvider` for production builds.
- On online launch: authenticate player, select authorized world, pull server snapshot, then apply to local rendering.
- Periodic sync: `GET /worlds/:worldId/sync?since=<checkpoint>` applies authorized diffs.
- Actions: `POST /worlds/:worldId/actions` sends player intent/action journal; server validates and returns canonical result.
- Chat/live mode: companion mobile reads `chat` and `live` endpoints for the same authorized world.

### 8.4 — Nintendo Local/Cloud Save Role

- Nintendo local save stores last-known authorized snapshot, pending offline actions, last sync checkpoint, device/session metadata, and player settings.
- Nintendo Cloud Save, if used, backs up only this Switch cache/recovery layer.
- The companion mobile app never reads Nintendo local save or Nintendo Cloud Save.
- Cached Switch data is treated as untrusted when uploaded; backend validates every queued action before changing canonical state.

### 8.5 — Startup and Sync Flow

```
1. Game opens on Switch.
2. GameStateManager authenticates the player.
3. If online:
   a. Pull authorized player/world state from backend.
   b. Create ServerWorldDataProvider.
   c. Apply server state to local rendering and refresh local cache.
4. If offline:
   a. Load cached snapshot from Nintendo local save.
   b. Create LocalWorldDataProvider in offline mode.
   c. Queue player actions in a local journal.
5. On reconnect:
   a. Upload pending journal to backend.
   b. Backend validates, resolves conflicts, and returns canonical state/diff.
   c. Switch cache is refreshed from canonical backend state.
```

### 8.6 — Pairing Companion Mobile

- Switch requests `POST /pairing/session` after the player selects "Link Companion App."
- Backend generates a one-time short-lived pairing token and returns QR/code content.
- Switch displays the QR/code; it does not generate or store secrets by itself.
- Mobile claims the code through `POST /pairing/claim`, then receives access only to the linked player's authorized worlds.
- Pairing codes expire quickly and are single-use.

### 8.7 — Authentication and AP Integrity

- Per GDD: passive AP uses verified account/server time rather than the console's manually adjustable clock.
- Server validates AP accrual, intervention costs, chat AP spending, and skill/AP rewards.
- Sensitive writes use backend validation only; clients do not directly grant AP, rewards, or irreversible world changes.

---

## Phase 9: Investor Vertical Slice Polish

**Goal:** Everything needed for the investor demo in 3 bubbles.

### 9.1 — Opening Sequence

- Harsh starting condition: dying apple tree, dead deer, dwindling stream (per GDD) ✅ implemented (`BubbleStarterFactory.CreateMeadow`)
- Tutorial sequence: hunger → discover deer → gather fuel → start fire → prepare food → eat → build shelter ✅ implemented (`TutorialSequence`, session-only; each step has a gameplay condition + timeout so the demo never stalls)
- First skill: Fire-making ✅ **resolved**

### 9.2 — Soulform Communication

- Text + voice input/output (per GDD topic 11) ✅ implemented — mic dictation button in chat (Windows `DictationRecognizer`) + SAPI text-to-speech (`VoiceService`); degrades gracefully to text-only where speech services are unavailable
- Emotion tags drive facial animation + voice modulation
- "The Soulform seems distant" when AP insufficient ✅ shown in chat and event log
- Free initiation requests, paid conversation ✅ chat opening is free; messages metered at full/half rate

### 9.3 — Intervention Demo

- 5 starting interventions (per GDD topic 6) ✅ AddFood, GrowTree, ChangeWeather, DayNight, SpawnStructure; SpawnAnimal, SpawnNpc, RemindSoulform are locked
- Unlock additional after first skill learned ✅ unlock on `SkillProgression.OnSkillAcquired`
- Show AP deduction, balance display, insufficient AP message ✅ live-screen AP label + chat header AP wired to real balance
- At least one gameplay event that awards AP (per GDD) ✅ skill unlock/level-up rewards

### 9.4 — Persistence & Memory Demo

- Play for 2 weeks, return, Soulform remembers past events
- Memory chapter system visible to player
- Skill progression observable over multiple sessions

### 9.5 — AI Model (Decision Needed)

- GDD topic 21: ✅ **resolved** — Llama 3.1 8B
- Hallucination boundaries active (3-check system per GDD topic 27) ✅ implemented in `SoulformBrainService.RunHallucinationChecks` (Relevance / Reality / Permission → silent rewrite retry → in-character self-correct)
- Hybrid update frequency (immediate + routine per GDD topic 28)

---

## Open Decisions Requiring Input

**Status: all 8 decisions resolved (2026-09-28).** Original options kept for reference:

| # | Decision | GDD Topic | Options | Resolution |
|---|----------|-----------|---------|------------|
| 1 | **First skill** | 3 | Fire-making or Foraging/Gardening? | **Fire-making** (fits the opening survival arc) |
| 2 | **Number of bubbles in demo** | 9 | 1, 3, or 5? | **3 bubbles** |
| 3 | **AI model** | 21 | Llama 3.1 8B / other? | **Llama 3.1 8B** |
| 4 | **Time system** | 16 | Real-time by timezone / Accelerated (x2) / Speed selector? | **Speed selector (1x/2x/4x)**, keep 1:60 ratio |
| 5 | **Starting condition** | 1 | Completely dry / Dying tree + dead deer + dwindling stream? | **Dying tree + dead deer + dwindling stream** |
| 6 | **Soulform naming** | 12 | Player-given or SF-generated? | **Player-given** |
| 7 | **Data sent to AI** | 18 | Exact numbers (32°C) or natural observations ("dangerously hot")? | **Exact numbers** |
| 8 | **Summer weather fix** | Weather table | Windy range 71-93 overlaps with Random 81-93 — intended? | **Keep current fix** (Windy 71-80, Random 81-93 — already correct in code) |

Additional Phase 9 scope decisions:

- **Voice I/O is in scope for 9.2** (overrides the ecosystem plan's text-only out-of-scope list): mic dictation + text-to-speech implemented in `VoiceService` with graceful text-only fallback.
- **Intervention starting set**: first 5 `InterventionType` values start unlocked (AddFood, GrowTree, ChangeWeather, DayNight, SpawnStructure); the remaining 3 unlock on the first skill learned.

---

## Recommended Implementation Order

| Priority | Phase | Estimated Effort | Dependencies |
|----------|-------|-----------------|--------------|
| 1 | Phase 0: Server-Ready Data Layer | 3-5 days | None |
| 2 | Phase 1: Entity Management | 5-7 days | Phase 0 |
| 3 | Phase 2: NPC/Animal Simulation | 5-7 days | Phase 1 |
| 4 | Phase 3: Environment & Water | 3-5 days | Phase 0 |
| 5 | Phase 4: Soulform Actions | 5-7 days | Phase 1, 2 |
| 6 | Phase 5: Weather Visuals | 3-5 days | Phase 0 |
| 7 | Phase 6: Time Progression | 2-3 days | Phase 0 |
| 8 | Phase 7: Multi-Bubble | 5-7 days | Phase 1, 3 |
| 9 | Phase 8: Backend Server | 15-25 days | Phase 0, save/entity state, companion scope |
| 10 | Phase 9: Polish | 5-7 days | All above |

**Total estimate: ~45-60 days for vertical slice (Phases 0-7, 9)**

**Backend server (Phase 8) is required for Nintendo + companion mobile chat/live mode.**

The server-authoritative abstraction in Phase 0 is the critical first step — it keeps player-specific game state compatible with backend-primary saves, Nintendo cache/recovery, and companion mobile access.
