import { db } from '../db';

// World-state single source of truth for the Unity client.
//
// The client's snapshot is an OPAQUE string (the JSON of its own
// BubbleWorldSaveData blob) — its shape is owned by the Unity side, so this
// service only versions it and hands it back. On server boot, preload() loads
// every row into memory so the backend comes up already holding the
// authoritative state; reads serve from that cache, writes go through to
// Postgres and refresh it.

export interface WorldSyncSnapshot {
  worldKey: string;
  version: number;
  state: string | null;
  updatedAt: string | null;
}

const cache = new Map<string, WorldSyncSnapshot>();

function toSnapshot(row: {
  world_key: string;
  version: number;
  state: string | null;
  updated_at: Date | string;
}): WorldSyncSnapshot {
  return {
    worldKey: row.world_key,
    version: row.version,
    state: row.state,
    updatedAt: new Date(row.updated_at).toISOString(),
  };
}

export async function preload(): Promise<void> {
  const res = await db.query(
    'SELECT world_key, version, state, updated_at FROM client_world_states'
  );
  cache.clear();
  for (const row of res.rows) {
    const snap = toSnapshot(row);
    cache.set(snap.worldKey, snap);
  }
  console.log(`[world-sync] loaded ${res.rows.length} world state(s) into memory`);
}

export async function getState(worldKey: string): Promise<WorldSyncSnapshot> {
  const hit = cache.get(worldKey);
  if (hit) return hit;

  // Cache miss (row created by another process) → DB fallback, then backfill.
  const res = await db.query(
    'SELECT world_key, version, state, updated_at FROM client_world_states WHERE world_key = $1',
    [worldKey]
  );
  if (res.rows.length === 0) {
    return { worldKey, version: 0, state: null, updatedAt: null };
  }
  const snap = toSnapshot(res.rows[0]);
  cache.set(worldKey, snap);
  return snap;
}

export async function putState(
  worldKey: string,
  state: string
): Promise<WorldSyncSnapshot> {
  const res = await db.query(
    `INSERT INTO client_world_states (world_key, version, state, updated_at)
     VALUES ($1, 1, $2, NOW())
     ON CONFLICT (world_key) DO UPDATE SET
       version = client_world_states.version + 1,
       state = EXCLUDED.state,
       updated_at = NOW()
     RETURNING world_key, version, state, updated_at`,
    [worldKey, state]
  );
  const snap = toSnapshot(res.rows[0]);
  cache.set(worldKey, snap);
  return snap;
}
