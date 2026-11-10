import { v4 as uuid } from 'uuid';
import { db } from '../db';
import { AppError } from '../middleware/errorHandler';
import { EntityData, NPCStateData, AnimalStateData, SoulformEntityStateData, BubbleData, BubbleConnectionData, CellData } from '../types';

export async function getEntities(worldId: string, entityType?: string): Promise<EntityData[]> {
  let query = 'SELECT * FROM entities WHERE world_id = $1';
  const params: unknown[] = [worldId];

  if (entityType) {
    query += ' AND entity_type = $2';
    params.push(entityType);
  }

  query += ' ORDER BY created_at ASC';

  const entities = await db.query(query, params);
  const result: EntityData[] = [];

  for (const e of entities.rows) {
    const entity: EntityData = {
      id: e.id,
      entityType: e.entity_type,
      gridX: e.grid_x,
      gridY: e.grid_y,
      gridZ: e.grid_z,
      facing: e.facing,
      health: parseFloat(e.health),
      hunger: parseFloat(e.hunger),
      pendingMovement: e.pending_movement,
      targetX: e.target_x,
      targetY: e.target_y,
      targetZ: e.target_z,
      targetEntityId: e.target_entity_id,
      isDead: e.is_dead,
    };

    if (e.entity_type === 'NPC') {
      const npc = await db.query('SELECT * FROM npc_states WHERE entity_id = $1', [e.id]);
      if (npc.rows.length > 0) {
        const n = npc.rows[0];
        entity.npcState = {
          baselineState: n.baseline_state,
          currentState: n.current_state,
          currentAction: n.current_action,
          isDisgruntled: n.is_disgruntled,
          disgruntledDays: n.disgruntled_days,
          consecutiveResourceShortageDays: n.consecutive_resource_shortage_days,
          hasAdequateResources: n.has_adequate_resources,
          hasShelter: n.has_shelter,
          consecutiveFoodDays: n.consecutive_food_days,
          consecutiveWaterDays: n.consecutive_water_days,
          consecutiveShelterDays: n.consecutive_shelter_days,
        };
      }
    } else if (e.entity_type === 'Animal') {
      const animal = await db.query('SELECT * FROM animal_states WHERE entity_id = $1', [e.id]);
      if (animal.rows.length > 0) {
        const a = animal.rows[0];
        entity.animalState = {
          species: a.species,
          diet: a.diet,
          hungerLevel: parseFloat(a.hunger_level),
          maxHunger: parseFloat(a.max_hunger),
          hungerRate: parseFloat(a.hunger_rate),
          currentState: a.current_state,
          isAttacked: a.is_attacked,
          socialCooldown: parseFloat(a.social_cooldown),
          currentFoodTarget: a.current_food_target,
        };
      }
    } else if (e.entity_type === 'Soulform') {
      const sf = await db.query('SELECT * FROM soulform_entity_states WHERE entity_id = $1', [e.id]);
      if (sf.rows.length > 0) {
        const s = sf.rows[0];
        entity.soulformEntityState = {
          currentSkillAttempt: s.current_skill_attempt,
          currentActivity: s.current_activity,
        };
      }
    }

    result.push(entity);
  }

  return result;
}

export async function getEntity(worldId: string, entityId: string): Promise<EntityData> {
  const entity = await db.query(
    'SELECT * FROM entities WHERE id = $1 AND world_id = $2',
    [entityId, worldId]
  );

  if (entity.rows.length === 0) {
    throw new AppError(404, 'Entity not found');
  }

  const e = entity.rows[0];
  const result: EntityData = {
    id: e.id,
    entityType: e.entity_type,
    gridX: e.grid_x,
    gridY: e.grid_y,
    gridZ: e.grid_z,
    facing: e.facing,
    health: parseFloat(e.health),
    hunger: parseFloat(e.hunger),
    pendingMovement: e.pending_movement,
    targetX: e.target_x,
    targetY: e.target_y,
    targetZ: e.target_z,
    targetEntityId: e.target_entity_id,
    isDead: e.is_dead,
  };

  if (e.entity_type === 'NPC') {
    const npc = await db.query('SELECT * FROM npc_states WHERE entity_id = $1', [e.id]);
    if (npc.rows.length > 0) {
      const n = npc.rows[0];
      result.npcState = {
        baselineState: n.baseline_state,
        currentState: n.current_state,
        currentAction: n.current_action,
        isDisgruntled: n.is_disgruntled,
        disgruntledDays: n.disgruntled_days,
        consecutiveResourceShortageDays: n.consecutive_resource_shortage_days,
        hasAdequateResources: n.has_adequate_resources,
        hasShelter: n.has_shelter,
        consecutiveFoodDays: n.consecutive_food_days,
        consecutiveWaterDays: n.consecutive_water_days,
        consecutiveShelterDays: n.consecutive_shelter_days,
      };
    }
  } else if (e.entity_type === 'Animal') {
    const animal = await db.query('SELECT * FROM animal_states WHERE entity_id = $1', [e.id]);
    if (animal.rows.length > 0) {
      const a = animal.rows[0];
      result.animalState = {
        species: a.species,
        diet: a.diet,
        hungerLevel: parseFloat(a.hunger_level),
        maxHunger: parseFloat(a.max_hunger),
        hungerRate: parseFloat(a.hunger_rate),
        currentState: a.current_state,
        isAttacked: a.is_attacked,
        socialCooldown: parseFloat(a.social_cooldown),
        currentFoodTarget: a.current_food_target,
      };
    }
  } else if (e.entity_type === 'Soulform') {
    const sf = await db.query('SELECT * FROM soulform_entity_states WHERE entity_id = $1', [e.id]);
    if (sf.rows.length > 0) {
      const s = sf.rows[0];
      result.soulformEntityState = {
        currentSkillAttempt: s.current_skill_attempt,
        currentActivity: s.current_activity,
      };
    }
  }

  return result;
}

export async function spawnEntity(
  worldId: string,
  entityType: string,
  data: {
    gridX?: number;
    gridY?: number;
    gridZ?: number;
    facing?: string;
    health?: number;
    hunger?: number;
    npcState?: Partial<NPCStateData>;
    animalState?: Partial<AnimalStateData>;
    soulformEntityState?: Partial<SoulformEntityStateData>;
  }
): Promise<EntityData> {
  const entityId = uuid();

  await db.query(
    `INSERT INTO entities (id, world_id, entity_type, grid_x, grid_y, grid_z, facing, health, hunger)
     VALUES ($1, $2, $3, $4, $5, $6, $7, $8, $9)`,
    [
      entityId, worldId, entityType,
      data.gridX ?? 0, data.gridY ?? 0, data.gridZ ?? 0,
      data.facing ?? 'South', data.health ?? 100, data.hunger ?? 50
    ]
  );

  if (entityType === 'NPC' && data.npcState) {
    await db.query(
      `INSERT INTO npc_states (entity_id, baseline_state, current_state, current_action,
         is_disgruntled, disgruntled_days, consecutive_resource_shortage_days,
         has_adequate_resources, has_shelter, consecutive_food_days, consecutive_water_days, consecutive_shelter_days)
       VALUES ($1, $2, $3, $4, $5, $6, $7, $8, $9, $10, $11, $12)`,
      [
        entityId,
        data.npcState.baselineState ?? 'Busy',
        data.npcState.currentState ?? 'SafeHungry',
        data.npcState.currentAction ?? 'None',
        data.npcState.isDisgruntled ?? false,
        data.npcState.disgruntledDays ?? 0,
        data.npcState.consecutiveResourceShortageDays ?? 0,
        data.npcState.hasAdequateResources ?? false,
        data.npcState.hasShelter ?? false,
        data.npcState.consecutiveFoodDays ?? 0,
        data.npcState.consecutiveWaterDays ?? 0,
        data.npcState.consecutiveShelterDays ?? 0,
      ]
    );
  } else if (entityType === 'Animal' && data.animalState) {
    await db.query(
      `INSERT INTO animal_states (entity_id, species, diet, hunger_level, max_hunger, hunger_rate,
         current_state, is_attacked, social_cooldown, current_food_target)
       VALUES ($1, $2, $3, $4, $5, $6, $7, $8, $9, $10)`,
      [
        entityId,
        data.animalState.species ?? 'Generic',
        data.animalState.diet ?? 'Omnivore',
        data.animalState.hungerLevel ?? 80,
        data.animalState.maxHunger ?? 100,
        data.animalState.hungerRate ?? 2,
        data.animalState.currentState ?? 'Docile',
        data.animalState.isAttacked ?? false,
        data.animalState.socialCooldown ?? 0,
        data.animalState.currentFoodTarget ?? null,
      ]
    );
  } else if (entityType === 'Soulform') {
    await db.query(
      `INSERT INTO soulform_entity_states (entity_id, current_activity)
       VALUES ($1, $2)`,
      [entityId, data.soulformEntityState?.currentActivity ?? 'Observing']
    );
  }

  return getEntity(worldId, entityId);
}

export async function updateEntity(
  worldId: string,
  entityId: string,
  updates: {
    gridX?: number;
    gridY?: number;
    gridZ?: number;
    facing?: string;
    health?: number;
    hunger?: number;
    pendingMovement?: string;
    targetX?: number | null;
    targetY?: number | null;
    targetZ?: number | null;
    targetEntityId?: string | null;
    isDead?: boolean;
    npcState?: Partial<NPCStateData>;
    animalState?: Partial<AnimalStateData>;
    soulformEntityState?: Partial<SoulformEntityStateData>;
  }
): Promise<EntityData> {
  const existing = await db.query(
    'SELECT id, entity_type FROM entities WHERE id = $1 AND world_id = $2',
    [entityId, worldId]
  );

  if (existing.rows.length === 0) {
    throw new AppError(404, 'Entity not found');
  }

  const allowedFields = [
    'grid_x', 'grid_y', 'grid_z', 'facing', 'health', 'hunger',
    'pending_movement', 'target_x', 'target_y', 'target_z',
    'target_entity_id', 'is_dead'
  ];

  const fieldMap: Record<string, string> = {
    gridX: 'grid_x', gridY: 'grid_y', gridZ: 'grid_z',
    facing: 'facing', health: 'health', hunger: 'hunger',
    pendingMovement: 'pending_movement',
    targetX: 'target_x', targetY: 'target_y', targetZ: 'target_z',
    targetEntityId: 'target_entity_id', isDead: 'is_dead',
  };

  const setClauses: string[] = ['updated_at = NOW()'];
  const params: unknown[] = [];
  let paramIndex = 1;

  for (const [key, value] of Object.entries(updates)) {
    if (fieldMap[key] && value !== undefined) {
      setClauses.push(`${fieldMap[key]} = $${paramIndex++}`);
      params.push(value);
    }
  }

  if (setClauses.length > 1) {
    params.push(entityId);
    await db.query(
      `UPDATE entities SET ${setClauses.join(', ')} WHERE id = $${paramIndex}`,
      params
    );
  }

  const entityType = existing.rows[0].entity_type;

  if (entityType === 'NPC' && updates.npcState) {
    await updateNPCState(entityId, updates.npcState);
  } else if (entityType === 'Animal' && updates.animalState) {
    await updateAnimalState(entityId, updates.animalState);
  } else if (entityType === 'Soulform' && updates.soulformEntityState) {
    await updateSoulformEntityState(entityId, updates.soulformEntityState);
  }

  return getEntity(worldId, entityId);
}

export async function removeEntity(worldId: string, entityId: string): Promise<void> {
  const result = await db.query(
    'DELETE FROM entities WHERE id = $1 AND world_id = $2 RETURNING id',
    [entityId, worldId]
  );

  if (result.rows.length === 0) {
    throw new AppError(404, 'Entity not found');
  }
}

async function updateNPCState(entityId: string, updates: Partial<NPCStateData>): Promise<void> {
  const fieldMap: Record<string, string> = {
    baselineState: 'baseline_state',
    currentState: 'current_state',
    currentAction: 'current_action',
    isDisgruntled: 'is_disgruntled',
    disgruntledDays: 'disgruntled_days',
    consecutiveResourceShortageDays: 'consecutive_resource_shortage_days',
    hasAdequateResources: 'has_adequate_resources',
    hasShelter: 'has_shelter',
    consecutiveFoodDays: 'consecutive_food_days',
    consecutiveWaterDays: 'consecutive_water_days',
    consecutiveShelterDays: 'consecutive_shelter_days',
  };

  const setClauses: string[] = ['updated_at = NOW()'];
  const params: unknown[] = [];
  let paramIndex = 1;

  for (const [key, value] of Object.entries(updates)) {
    if (fieldMap[key] && value !== undefined) {
      setClauses.push(`${fieldMap[key]} = $${paramIndex++}`);
      params.push(value);
    }
  }

  if (setClauses.length > 1) {
    params.push(entityId);
    await db.query(
      `UPDATE npc_states SET ${setClauses.join(', ')} WHERE entity_id = $${paramIndex}`,
      params
    );
  }
}

async function updateAnimalState(entityId: string, updates: Partial<AnimalStateData>): Promise<void> {
  const fieldMap: Record<string, string> = {
    species: 'species', diet: 'diet',
    hungerLevel: 'hunger_level', maxHunger: 'max_hunger',
    hungerRate: 'hunger_rate', currentState: 'current_state',
    isAttacked: 'is_attacked', socialCooldown: 'social_cooldown',
    currentFoodTarget: 'current_food_target',
  };

  const setClauses: string[] = ['updated_at = NOW()'];
  const params: unknown[] = [];
  let paramIndex = 1;

  for (const [key, value] of Object.entries(updates)) {
    if (fieldMap[key] && value !== undefined) {
      setClauses.push(`${fieldMap[key]} = $${paramIndex++}`);
      params.push(value);
    }
  }

  if (setClauses.length > 1) {
    params.push(entityId);
    await db.query(
      `UPDATE animal_states SET ${setClauses.join(', ')} WHERE entity_id = $${paramIndex}`,
      params
    );
  }
}

async function updateSoulformEntityState(entityId: string, updates: Partial<SoulformEntityStateData>): Promise<void> {
  const setClauses: string[] = ['updated_at = NOW()'];
  const params: unknown[] = [];
  let paramIndex = 1;

  if (updates.currentSkillAttempt !== undefined) {
    setClauses.push(`current_skill_attempt = $${paramIndex++}`);
    params.push(updates.currentSkillAttempt);
  }
  if (updates.currentActivity !== undefined) {
    setClauses.push(`current_activity = $${paramIndex++}`);
    params.push(updates.currentActivity);
  }

  if (setClauses.length > 1) {
    params.push(entityId);
    await db.query(
      `UPDATE soulform_entity_states SET ${setClauses.join(', ')} WHERE entity_id = $${paramIndex}`,
      params
    );
  }
}

export async function getBubbles(worldId: string): Promise<BubbleData[]> {
  const bubbles = await db.query(
    'SELECT * FROM bubbles WHERE world_id = $1 ORDER BY bubble_id',
    [worldId]
  );

  const connections = await db.query(
    'SELECT * FROM bubble_connections WHERE world_id = $1',
    [worldId]
  );

  return bubbles.rows.map((b: { bubble_id: string; environment_type: string; grid_width: number; grid_height: number; grid_depth: number; season_offset: number; is_unlocked: boolean }) => ({
    bubbleId: b.bubble_id,
    environmentType: b.environment_type,
    gridWidth: b.grid_width,
    gridHeight: b.grid_height,
    gridDepth: b.grid_depth,
    seasonOffset: b.season_offset,
    isUnlocked: b.is_unlocked,
    connections: connections.rows
      .filter((c: { from_bubble_id: string }) => c.from_bubble_id === b.bubble_id)
      .map((c: { from_bubble_id: string; to_bubble_id: string; direction: string; is_open: boolean }) => ({
        fromBubbleId: c.from_bubble_id,
        toBubbleId: c.to_bubble_id,
        direction: c.direction,
        isOpen: c.is_open,
      })),
  }));
}

export async function createBubble(
  worldId: string,
  bubbleId: string,
  environmentType: string,
  gridWidth: number,
  gridHeight: number,
  gridDepth: number,
  seasonOffset: number
): Promise<BubbleData> {
  await db.query(
    `INSERT INTO bubbles (world_id, bubble_id, environment_type, grid_width, grid_height, grid_depth, season_offset)
     VALUES ($1, $2, $3, $4, $5, $6, $7)`,
    [worldId, bubbleId, environmentType, gridWidth, gridHeight, gridDepth, seasonOffset]
  );

  const bubbles = await getBubbles(worldId);
  return bubbles.find(b => b.bubbleId === bubbleId)!;
}

export async function setTileCell(
  worldId: string,
  bubbleId: string,
  x: number,
  y: number,
  z: number,
  cell: {
    itemId?: string;
    layer?: number;
    isObject?: boolean;
    rotation?: number;
    autoTileVariant?: number;
    fromIntervention?: boolean;
    groundItemId?: string;
    tileCategory?: string;
    tileHeight?: string;
  }
): Promise<void> {
  await db.query(
    `INSERT INTO tile_cells (world_id, bubble_id, x, y, z, item_id, layer, is_object, rotation, auto_tile_variant, from_intervention, ground_item_id, tile_category, tile_height)
     VALUES ($1, $2, $3, $4, $5, $6, $7, $8, $9, $10, $11, $12, $13, $14)
     ON CONFLICT (world_id, bubble_id, x, y, z)
     DO UPDATE SET item_id = COALESCE($6, tile_cells.item_id),
                  layer = COALESCE($7, tile_cells.layer),
                  is_object = COALESCE($8, tile_cells.is_object),
                  rotation = COALESCE($9, tile_cells.rotation),
                  auto_tile_variant = COALESCE($10, tile_cells.auto_tile_variant),
                  from_intervention = COALESCE($11, tile_cells.from_intervention),
                  ground_item_id = COALESCE($12, tile_cells.ground_item_id),
                  tile_category = COALESCE($13, tile_cells.tile_category),
                  tile_height = COALESCE($14, tile_cells.tile_height)`,
    [worldId, bubbleId, x, y, z,
     cell.itemId ?? '', cell.layer ?? 0, cell.isObject ?? false,
     cell.rotation ?? 0, cell.autoTileVariant ?? 0, cell.fromIntervention ?? false,
     cell.groundItemId ?? '', cell.tileCategory ?? '', cell.tileHeight ?? 'Full']
  );
}

export async function removeTileCell(
  worldId: string,
  bubbleId: string,
  x: number,
  y: number,
  z: number
): Promise<boolean> {
  const result = await db.query(
    'DELETE FROM tile_cells WHERE world_id = $1 AND bubble_id = $2 AND x = $3 AND y = $4 AND z = $5',
    [worldId, bubbleId, x, y, z]
  );
  return (result.rowCount ?? 0) > 0;
}

export async function getTileCells(
  worldId: string,
  bubbleId: string
): Promise<Record<string, CellData>> {
  const cells = await db.query(
    'SELECT * FROM tile_cells WHERE world_id = $1 AND bubble_id = $2',
    [worldId, bubbleId]
  );

  const result: Record<string, CellData> = {};
  for (const c of cells.rows) {
    const key = `${c.x},${c.y},${c.z}`;
    result[key] = {
      x: c.x, y: c.y, z: c.z,
      itemId: c.item_id,
      layer: c.layer,
      isObject: c.is_object,
      rotation: c.rotation,
      autoTileVariant: c.auto_tile_variant,
      fromIntervention: c.from_intervention,
      groundItemId: c.ground_item_id,
      tileCategory: c.tile_category,
      tileHeight: c.tile_height,
    };
  }
  return result;
}
