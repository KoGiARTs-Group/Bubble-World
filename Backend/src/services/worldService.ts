import { v4 as uuid } from 'uuid';
import zlib from 'zlib';
import { db } from '../db';
import { config } from '../config';
import { AppError } from '../middleware/errorHandler';
import { WorldStateData, ActionData, ActionResult, LiveSummaryData, VALID_WEATHER_TYPES } from '../types';
import * as entityService from './entityService';

export async function getWorldsForAccount(accountId: string) {
  const owned = await db.query(
    `SELECT w.id, w.name, w.environment_type, w.current_version, w.day_count,
            w.current_season, w.weather_condition, w.livability_percent, w.updated_at,
            'owner' as role
     FROM worlds w
     WHERE w.owner_account_id = $1
     ORDER BY w.updated_at DESC`,
    [accountId]
  );

  const shared = await db.query(
    `SELECT w.id, w.name, w.environment_type, w.current_version, w.day_count,
            w.current_season, w.weather_condition, w.livability_percent, w.updated_at,
            wm.role
     FROM worlds w
     JOIN world_members wm ON w.id = wm.world_id
     WHERE wm.account_id = $1
     ORDER BY w.updated_at DESC`,
    [accountId]
  );

  return [...owned.rows, ...shared.rows];
}

export async function createWorld(
  accountId: string,
  name: string,
  environmentType: string,
  gridWidth: number,
  gridHeight: number,
  gridDepth: number
) {
  const worldId = uuid();

  const result = await db.query(
    `INSERT INTO worlds (id, owner_account_id, name, environment_type, grid_width, grid_height, grid_depth)
     VALUES ($1, $2, $3, $4, $5, $6, $7)
     RETURNING *`,
    [worldId, accountId, name, environmentType, gridWidth, gridHeight, gridDepth]
  );

  const world = result.rows[0];

  await db.query(
    `INSERT INTO ap_balances (world_id, account_id, balance)
     VALUES ($1, $2, $3)`,
    [worldId, accountId, config.ap.startingBalance]
  );

  await db.query(
    `INSERT INTO soulform_states (world_id) VALUES ($1)`,
    [worldId]
  );

  await db.query(
    `INSERT INTO memory_chapters (world_id, chapter_index, start_time)
     VALUES ($1, 0, NOW())`,
    [worldId]
  );

  await db.query(
    `UPDATE player_stats SET worlds_created = worlds_created + 1 WHERE account_id = $1`,
    [accountId]
  );

  return world;
}

export async function getWorldState(accountId: string, worldId: string): Promise<WorldStateData> {
  await verifyWorldAccess(accountId, worldId);

  const world = await db.query('SELECT * FROM worlds WHERE id = $1', [worldId]);
  if (world.rows.length === 0) {
    throw new AppError(404, 'World not found');
  }

  const latestSnapshot = await db.query(
    `SELECT compressed_state FROM world_state_snapshots
     WHERE world_id = $1
     ORDER BY version DESC LIMIT 1`,
    [worldId]
  );

  if (latestSnapshot.rows.length === 0) {
    return buildWorldStateFromDB(worldId);
  }

  const decompressed = decompressState(latestSnapshot.rows[0].compressed_state);
  return decompressed as WorldStateData;
}

export async function uploadWorldState(
  accountId: string,
  worldId: string,
  state: WorldStateData
): Promise<{ version: number; checkpoint: string }> {
  await verifyWorldAccess(accountId, worldId, 'owner');

  const world = await db.query(
    'SELECT current_version FROM worlds WHERE id = $1 FOR UPDATE',
    [worldId]
  );

  if (world.rows.length === 0) {
    throw new AppError(404, 'World not found');
  }

  const newVersion = (world.rows[0]?.current_version || 0) + 1;
  const checkpoint = uuid();
  const compressed = compressState(state);

  await db.query(
    `INSERT INTO world_state_snapshots (world_id, version, compressed_state, checkpoint)
     VALUES ($1, $2, $3, $4)`,
    [worldId, newVersion, compressed, checkpoint]
  );

  await db.query(
    `UPDATE worlds SET
       current_version = $1, current_checkpoint = $2,
       day_count = $3, current_season = $4, weather_condition = $5,
       livability_percent = $6, updated_at = NOW()
     WHERE id = $7`,
    [
      newVersion, checkpoint, state.dayCount, state.currentSeason,
      state.weatherCondition, state.livabilityPercent, worldId
    ]
  );

  return { version: newVersion, checkpoint };
}

async function buildWorldStateFromDB(worldId: string): Promise<WorldStateData> {
  const world = await db.query('SELECT * FROM worlds WHERE id = $1', [worldId]);
  const w = world.rows[0];

  const soulform = await db.query(
    'SELECT * FROM soulform_states WHERE world_id = $1', [worldId]
  );

  const ap = await db.query(
    'SELECT balance, last_passive_ap_time FROM ap_balances WHERE world_id = $1', [worldId]
  );

  const entities = await entityService.getEntities(worldId);
  const bubbles = await entityService.getBubbles(worldId);
  const cells = await entityService.getTileCells(worldId, 'main');

  const sf = soulform.rows[0] || {};
  const apData = ap.rows[0] || {};

  return {
    saveVersion: '2.1',
    worldName: w.name,
    environmentType: w.environment_type,
    gridWidth: w.grid_width,
    gridHeight: w.grid_height,
    gridDepth: w.grid_depth,
    cells,
    livabilityPercent: w.livability_percent || 0,
    weatherCondition: w.weather_condition || 'Clear',
    soulform: {
      name: sf.name || 'Soulform',
      meaning: sf.meaning || '',
      nature: sf.nature || 'Explorer',
      temperament: sf.temperament || 'Inquisitive',
      mood: sf.mood || 'Content',
      gridX: sf.grid_x ?? 12,
      gridY: sf.grid_y ?? 0,
      gridZ: sf.grid_z ?? 12,
      comfort: sf.comfort ?? 50,
      hunger: sf.hunger ?? 50,
      curiosity: sf.curiosity ?? 50,
      health: sf.health ?? 100,
      currentActivity: sf.current_activity || 'Observing',
      currentThought: sf.current_thought || 'Taking in the surroundings.',
      currentExpression: sf.current_expression || 'Neutral',
      currentAnimation: sf.current_animation || 'Idle',
      appearanceId: sf.appearance?.appearanceId || 'default',
      bodyTypeId: sf.appearance?.bodyTypeId || 'average',
      heightId: sf.appearance?.heightId || 'average',
      faceShapeId: sf.appearance?.faceShapeId || 'default',
      eyesId: sf.appearance?.eyesId || 'default',
      earsId: sf.appearance?.earsId || 'default',
      noseId: sf.appearance?.noseId || 'default',
      skinToneR: sf.appearance?.skinToneR ?? 1,
      skinToneG: sf.appearance?.skinToneG ?? 1,
      skinToneB: sf.appearance?.skinToneB ?? 1,
      markingsId: sf.appearance?.markingsId || 'none',
      skinDetailsId: sf.appearance?.skinDetailsId || 'none',
      hairStyleId: sf.appearance?.hairStyleId || 'default',
      hairColorR: sf.appearance?.hairColorR ?? 0.3,
      hairColorG: sf.appearance?.hairColorG ?? 0.2,
      hairColorB: sf.appearance?.hairColorB ?? 0.15,
      hairMaterialId: sf.appearance?.hairMaterialId || 'default',
      facialHairId: sf.appearance?.facialHairId || 'none',
      voicePresetId: sf.voice?.voicePresetId || 'default',
      voicePitch: sf.voice?.voicePitch ?? 1,
      voiceTone: sf.voice?.voiceTone ?? 1,
      voiceTimbre: sf.voice?.voiceTimbre ?? 1,
      outfitId: sf.outfit?.outfitId || '',
      topId: sf.outfit?.topId || '',
      bottomsId: sf.outfit?.bottomsId || '',
      footwearId: sf.outfit?.footwearId || '',
      accessoryId: sf.outfit?.accessoryId || '',
      startingAffinity: sf.starting_affinity ?? 0.5,
      initialCalling: sf.initial_calling || '',
      preferredBiome: sf.preferred_biome || 'MeadowGrassland',
      bondTraits: sf.bond_traits || [],
    },
    dayCount: w.day_count || 1,
    currentSeason: w.current_season || 0,
    architectPoints: apData.balance || config.ap.startingBalance,
    lastPassiveAPTime: apData.last_passive_ap_time?.toISOString() || new Date().toISOString(),
    skillData: '{}',
    memoryChapters: '[]',
    tileCompositions: {},
    tileStates: {},
    cooldowns: [],
    entities,
    bubbles,
  };
}

export async function saveWorldSnapshot(
  worldId: string,
  state: WorldStateData
): Promise<{ version: number; checkpoint: string }> {
  const world = await db.query(
    'SELECT current_version FROM worlds WHERE id = $1 FOR UPDATE',
    [worldId]
  );

  const newVersion = (world.rows[0]?.current_version || 0) + 1;
  const checkpoint = uuid();
  const compressed = compressState(state);

  await db.query(
    `INSERT INTO world_state_snapshots (world_id, version, compressed_state, checkpoint)
     VALUES ($1, $2, $3, $4)`,
    [worldId, newVersion, compressed, checkpoint]
  );

  await db.query(
    `UPDATE worlds SET current_version = $1, current_checkpoint = $2, updated_at = NOW()
     WHERE id = $3`,
    [newVersion, checkpoint, worldId]
  );

  return { version: newVersion, checkpoint };
}

export async function submitActions(
  accountId: string,
  worldId: string,
  actions: ActionData[]
): Promise<{ results: ActionResult[]; newVersion?: number; checkpoint?: string }> {
  await verifyWorldAccess(accountId, worldId, 'member');

  const results: ActionResult[] = [];
  const client = await db.getClient();

  try {
    await client.query('BEGIN');

    for (let i = 0; i < actions.length; i++) {
      const action = actions[i];
      const actionId = uuid();

      const validation = validateAction(action);
      if (!validation.valid) {
        results.push({
          actionId,
          accepted: false,
          reason: validation.reason,
        });

        await client.query(
          `INSERT INTO action_journal_entries
           (id, world_id, account_id, action_type, action_data, validation_result, batch_id, sequence_index)
           VALUES ($1, $2, $3, $4, $5, 'rejected', $6, $7)`,
          [
            actionId, worldId, accountId, action.actionType,
            JSON.stringify(action.actionData), action.batchId || null, action.sequenceIndex ?? i
          ]
        );
        continue;
      }

      const serverResult = await applyAction(client, worldId, action);

      results.push({
        actionId,
        accepted: true,
        serverResult,
      });

      await client.query(
        `INSERT INTO action_journal_entries
         (id, world_id, account_id, action_type, action_data, validation_result, server_result,
          client_timestamp, batch_id, sequence_index)
         VALUES ($1, $2, $3, $4, $5, 'accepted', $6, $7, $8, $9)`,
        [
          actionId, worldId, accountId, action.actionType,
          JSON.stringify(action.actionData), JSON.stringify(serverResult),
          action.clientTimestamp || null, action.batchId || null, action.sequenceIndex ?? i
        ]
      );
    }

    await client.query('COMMIT');
    return { results };
  } catch (err) {
    await client.query('ROLLBACK');
    throw err;
  } finally {
    client.release();
  }
}

export async function getSyncDiff(
  accountId: string,
  worldId: string,
  sinceCheckpoint: string
): Promise<{ type: 'diff' | 'full'; data: unknown; version: number; checkpoint: string }> {
  await verifyWorldAccess(accountId, worldId);

  const checkpointRow = await db.query(
    `SELECT version FROM world_state_snapshots WHERE checkpoint = $1 AND world_id = $2`,
    [sinceCheckpoint, worldId]
  );

  if (checkpointRow.rows.length === 0) {
    return getFullSnapshot(accountId, worldId);
  }

  const sinceVersion = checkpointRow.rows[0].version;
  const world = await db.query(
    'SELECT current_version, current_checkpoint FROM worlds WHERE id = $1',
    [worldId]
  );

  const currentVersion = world.rows[0]?.current_version || 0;
  const currentCheckpoint = world.rows[0]?.current_checkpoint;

  if (sinceVersion === currentVersion) {
    return {
      type: 'diff',
      data: { changes: [] },
      version: currentVersion,
      checkpoint: currentCheckpoint,
    };
  }

  const diffs = await db.query(
    `SELECT from_version, to_version, compressed_diff FROM world_state_diffs
     WHERE world_id = $1 AND from_version >= $2 AND to_version <= $3
     ORDER BY from_version ASC`,
    [worldId, sinceVersion, currentVersion]
  );

  const expectedDiffs = currentVersion - sinceVersion;
  if (diffs.rows.length < expectedDiffs) {
    return getFullSnapshot(accountId, worldId);
  }

  const mergedDiffs = diffs.rows.map((d: { from_version: number; to_version: number; compressed_diff: Buffer }) => ({
    fromVersion: d.from_version,
    toVersion: d.to_version,
    diff: decompressData(d.compressed_diff),
  }));

  return {
    type: 'diff',
    data: { diffs: mergedDiffs },
    version: currentVersion,
    checkpoint: currentCheckpoint,
  };
}

export async function getLiveSummary(accountId: string, worldId: string): Promise<LiveSummaryData> {
  await verifyWorldAccess(accountId, worldId);

  const world = await db.query('SELECT * FROM worlds WHERE id = $1', [worldId]);
  if (world.rows.length === 0) {
    throw new AppError(404, 'World not found');
  }
  const w = world.rows[0];

  const soulform = await db.query(
    'SELECT * FROM soulform_states WHERE world_id = $1',
    [worldId]
  );
  const sf = soulform.rows[0] || {};

  const ap = await db.query(
    'SELECT balance FROM ap_balances WHERE world_id = $1 AND account_id = $2',
    [worldId, accountId]
  );

  const events = await db.query(
    `SELECT event_type, description, server_timestamp FROM world_event_log
     WHERE world_id = $1
     ORDER BY server_timestamp DESC LIMIT 10`,
    [worldId]
  );

  const entityCount = await db.query(
    `SELECT COUNT(*) as count FROM entities WHERE world_id = $1 AND is_dead = FALSE`,
    [worldId]
  );

  return {
    worldName: w.name,
    environmentType: w.environment_type,
    dayCount: w.day_count,
    season: ['Spring', 'Summer', 'Autumn', 'Winter'][w.current_season] || 'Spring',
    weather: w.weather_condition || 'Clear',
    livability: w.livability_percent || 0,
    population: parseInt((entityCount.rows[0] as { count: string })?.count || '0', 10),
    soulform: {
      name: sf.name || 'Soulform',
      mood: sf.mood || 'Content',
      currentActivity: sf.current_activity || 'Observing',
      currentThought: sf.current_thought || 'Taking in the surroundings.',
      health: sf.health || 100,
      comfort: sf.comfort || 50,
      hunger: sf.hunger || 50,
      curiosity: sf.curiosity || 50,
    },
    recentEvents: events.rows.map((e: { event_type: string; description: string; server_timestamp: Date }) => ({
      eventType: e.event_type,
      description: e.description,
      timestamp: e.server_timestamp.toISOString(),
    })),
    apBalance: ap.rows[0]?.balance || 0,
  };
}

async function getFullSnapshot(accountId: string, worldId: string) {
  const state = await getWorldState(accountId, worldId);
  const world = await db.query(
    'SELECT current_version, current_checkpoint FROM worlds WHERE id = $1',
    [worldId]
  );
  return {
    type: 'full' as const,
    data: state,
    version: world.rows[0]?.current_version || 0,
    checkpoint: world.rows[0]?.current_checkpoint,
  };
}

async function verifyWorldAccess(
  accountId: string,
  worldId: string,
  minRole: 'owner' | 'member' | 'viewer' = 'viewer'
): Promise<void> {
  const world = await db.query(
    'SELECT owner_account_id FROM worlds WHERE id = $1',
    [worldId]
  );

  if (world.rows.length === 0) {
    throw new AppError(404, 'World not found');
  }

  if (world.rows[0].owner_account_id === accountId) return;

  const member = await db.query(
    'SELECT role FROM world_members WHERE world_id = $1 AND account_id = $2',
    [worldId, accountId]
  );

  if (member.rows.length === 0) {
    throw new AppError(403, 'Not authorized for this world');
  }

  const hierarchy: Record<string, number> = { viewer: 0, member: 1, owner: 2 };
  const userRole = member.rows[0].role;
  if ((hierarchy[userRole] || 0) < (hierarchy[minRole] || 0)) {
    throw new AppError(403, 'Insufficient permissions');
  }
}

interface ActionValidation {
  valid: boolean;
  reason?: string;
}

function validateAction(action: ActionData): ActionValidation {
  const validActionTypes = [
    'place_tile', 'remove_tile', 'apply_brush',
    'intervention', 'entity_spawn', 'entity_move',
    'soulform_action', 'weather_change', 'day_advance',
    'chat_message', 'skill_attempt', 'memory_event',
  ];

  if (!validActionTypes.includes(action.actionType)) {
    return { valid: false, reason: `Unknown action type: ${action.actionType}` };
  }

  if (!action.actionData || typeof action.actionData !== 'object') {
    return { valid: false, reason: 'Action data is required' };
  }

  return { valid: true };
}

async function applyAction(
  client: { query: (text: string, params?: unknown[]) => Promise<{ rows: unknown[] }> },
  worldId: string,
  action: ActionData
): Promise<Record<string, unknown>> {
  switch (action.actionType) {
    case 'place_tile': {
      const { x, y, z, itemId, layer, bubbleId } = action.actionData as { x: number; y: number; z: number; itemId: string; layer: number; bubbleId?: string };
      await entityService.setTileCell(worldId, bubbleId || 'main', x, y, z, { itemId, layer });
      return { placed: true, x, y, z, itemId, layer };
    }
    case 'remove_tile': {
      const { x, y, z, bubbleId } = action.actionData as { x: number; y: number; z: number; bubbleId?: string };
      await entityService.removeTileCell(worldId, bubbleId || 'main', x, y, z);
      return { removed: true, x, y, z };
    }
    case 'apply_brush': {
      const { cells, bubbleId } = action.actionData as { cells: Array<{ x: number; y: number; z: number; itemId: string; layer: number }>; bubbleId?: string };
      const bid = bubbleId || 'main';
      for (const cell of cells) {
        await entityService.setTileCell(worldId, bid, cell.x, cell.y, cell.z, { itemId: cell.itemId, layer: cell.layer });
      }
      return { brushApplied: true, cellCount: cells.length };
    }
    case 'intervention': {
      const { interventionType } = action.actionData as { interventionType: string };
      const apResult = await client.query(
        'SELECT balance FROM ap_balances WHERE world_id = $1 FOR UPDATE',
        [worldId]
      );
      const balance = (apResult.rows[0] as { balance: number } | undefined)?.balance || 0;
      if (balance < config.ap.interventionCost) {
        throw new AppError(400, 'Insufficient AP for intervention');
      }
      await client.query(
        'UPDATE ap_balances SET balance = balance - $1, updated_at = NOW() WHERE world_id = $2',
        [config.ap.interventionCost, worldId]
      );
      return { interventionApplied: true, type: interventionType, apSpent: config.ap.interventionCost };
    }
    case 'entity_spawn': {
      const { entityType, gridX, gridY, gridZ, facing, health, hunger, npcState, animalState, soulformEntityState } = action.actionData as {
        entityType: string; gridX?: number; gridY?: number; gridZ?: number;
        facing?: string; health?: number; hunger?: number;
        npcState?: Record<string, unknown>; animalState?: Record<string, unknown>; soulformEntityState?: Record<string, unknown>;
      };
      const entity = await entityService.spawnEntity(worldId, entityType, {
        gridX, gridY, gridZ, facing, health, hunger,
        npcState: npcState as Parameters<typeof entityService.spawnEntity>[2]['npcState'],
        animalState: animalState as Parameters<typeof entityService.spawnEntity>[2]['animalState'],
        soulformEntityState: soulformEntityState as Parameters<typeof entityService.spawnEntity>[2]['soulformEntityState'],
      });
      return { entitySpawned: true, entityId: entity.id };
    }
    case 'entity_move': {
      const { entityId, gridX, gridY, gridZ, facing } = action.actionData as {
        entityId: string; gridX?: number; gridY?: number; gridZ?: number; facing?: string;
      };
      await entityService.updateEntity(worldId, entityId, { gridX, gridY, gridZ, facing });
      return { entityMoved: true, entityId, gridX, gridY, gridZ };
    }
    case 'skill_attempt': {
      const { skillName, success } = action.actionData as { skillName: string; success: boolean };
      return { skillAttempted: true, skillName, success };
    }
    case 'soulform_action': {
      const { action: sfAction, targetX, targetY, targetZ } = action.actionData as { action: string; targetX?: number; targetY?: number; targetZ?: number };
      await client.query(
        `UPDATE soulform_states SET current_activity = $1, updated_at = NOW() WHERE world_id = $2`,
        [sfAction, worldId]
      );
      if (targetX !== undefined && targetY !== undefined && targetZ !== undefined) {
        await client.query(
          `UPDATE soulform_states SET grid_x = $1, grid_y = $2, grid_z = $3 WHERE world_id = $4`,
          [targetX, targetY, targetZ, worldId]
        );
      }
      return { soulformAction: sfAction };
    }
    case 'weather_change': {
      const { weather } = action.actionData as { weather: string };
      if (!VALID_WEATHER_TYPES.includes(weather as typeof VALID_WEATHER_TYPES[number])) {
        return { weatherChanged: false, reason: `Invalid weather type: ${weather}` };
      }
      await client.query(
        'UPDATE worlds SET weather_condition = $1, updated_at = NOW() WHERE id = $2',
        [weather, worldId]
      );
      return { weatherChanged: true, newWeather: weather };
    }
    case 'day_advance': {
      const { newDay } = action.actionData as { newDay: number };
      const newSeason = Math.floor((newDay / 30) % 4);
      await client.query(
        'UPDATE worlds SET day_count = $1, current_season = $2, updated_at = NOW() WHERE id = $3',
        [newDay, newSeason, worldId]
      );
      return { dayAdvanced: true, newDay, newSeason };
    }
    case 'memory_event': {
      const { description, category } = action.actionData as { description: string; category: string };
      const chapter = await client.query(
        `SELECT id FROM memory_chapters WHERE world_id = $1 ORDER BY chapter_index DESC LIMIT 1`,
        [worldId]
      );
      if (chapter.rows.length > 0) {
        const chapterId = (chapter.rows[0] as { id: string }).id;
        await client.query(
          `INSERT INTO memory_entries (chapter_id, description, category) VALUES ($1, $2, $3)`,
          [chapterId, description, category]
        );
      }
      return { memoryRecorded: true };
    }
    default:
      return { applied: true, actionType: action.actionType };
  }
}

function compressState(state: WorldStateData): Buffer {
  const json = JSON.stringify(state);
  return zlib.gzipSync(Buffer.from(json, 'utf-8'));
}

function decompressState(data: Buffer): WorldStateData {
  return JSON.parse(zlib.gunzipSync(data).toString('utf-8'));
}

function decompressData(data: Buffer): unknown {
  return JSON.parse(zlib.gunzipSync(data).toString('utf-8'));
}
