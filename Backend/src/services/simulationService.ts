import zlib from 'zlib';
import { db } from '../db';
import { config } from '../config';
import { AppError } from '../middleware/errorHandler';
import { VALID_WEATHER_TYPES, SEASONAL_WEATHER_TABLE, WeatherType } from '../types';

export async function processTick(
  worldId: string,
  accountId: string,
  tickType: string,
  offlineDurationHours?: number
): Promise<{ tickResult: Record<string, unknown>; version: number }> {
  const world = await db.query(
    'SELECT current_version, day_count, current_season, weather_condition FROM worlds WHERE id = $1 FOR UPDATE',
    [worldId]
  );

  if (world.rows.length === 0) {
    throw new AppError(404, 'World not found');
  }

  const w = world.rows[0];
  const result: Record<string, unknown> = {
    tickType,
    previousDay: w.day_count,
  };

  if (tickType === 'offline_catchup' && offlineDurationHours) {
    result.offlineCatchup = await processOfflineCatchup(worldId, accountId, offlineDurationHours);
  }

  const newDay = w.day_count + 1;
  const newSeason = Math.floor((newDay / 30) % 4);
  await db.query(
    'UPDATE worlds SET day_count = $1, current_season = $2, updated_at = NOW() WHERE id = $3',
    [newDay, newSeason, worldId]
  );
  result.dayAdvanced = { previousDay: w.day_count, newDay, newSeason };

  const weatherResult = await processWeatherRoll(worldId, newSeason);
  result.weatherRoll = weatherResult;

  const passiveAP = await processPassiveAP(worldId, accountId);
  result.passiveAP = passiveAP;

  await processSoulformTick(worldId);
  result.soulformTicked = true;

  await processEntityTicks(worldId);
  result.entitiesTicked = true;

  await processSourceLogicTick(worldId);
  result.sourceLogicTicked = true;

  const newVersion = w.current_version + 1;
  await db.query(
    'UPDATE worlds SET current_version = $1, updated_at = NOW() WHERE id = $2',
    [newVersion, worldId]
  );

  result.newVersion = newVersion;
  return { tickResult: result, version: newVersion };
}

async function processWeatherRoll(worldId: string, season: number): Promise<{ newWeather: string | null; changed: boolean }> {
  const table = SEASONAL_WEATHER_TABLE[season] || SEASONAL_WEATHER_TABLE[0];
  const totalWeight = table.reduce((sum, entry) => sum + entry.weight, 0);
  let roll = Math.random() * totalWeight;
  let newWeather: WeatherType = 'Clear';

  for (const entry of table) {
    roll -= entry.weight;
    if (roll <= 0) {
      newWeather = entry.weather;
      break;
    }
  }

  const current = await db.query(
    'SELECT weather_condition FROM worlds WHERE id = $1',
    [worldId]
  );

  const currentWeather = current.rows[0]?.weather_condition || 'Clear';
  if (currentWeather === newWeather) {
    return { newWeather: null, changed: false };
  }

  if (!VALID_WEATHER_TYPES.includes(newWeather)) {
    newWeather = 'Clear';
  }

  await db.query(
    'UPDATE worlds SET weather_condition = $1, updated_at = NOW() WHERE id = $2',
    [newWeather, worldId]
  );

  await db.query(
    `INSERT INTO weather_history (world_id, weather_type, season, day_count)
     VALUES ($1, $2, $3, (SELECT day_count FROM worlds WHERE id = $1))`,
    [worldId, newWeather, season]
  );

  await db.query(
    `UPDATE weather_history SET ended_at = NOW()
     WHERE world_id = $1 AND ended_at IS NULL AND weather_type != $2`,
    [worldId, newWeather]
  );

  return { newWeather, changed: true };
}

async function processEntityTicks(worldId: string): Promise<void> {
  const animals = await db.query(
    `SELECT e.id, e.hunger, e.is_dead, a.hunger_level, a.max_hunger, a.hunger_rate, a.current_state, a.species
     FROM entities e
     JOIN animal_states a ON e.id = a.entity_id
     WHERE e.world_id = $1 AND e.is_dead = FALSE AND e.entity_type = 'Animal'`,
    [worldId]
  );

  for (const animal of animals.rows) {
    const newHunger = Math.max(0, parseFloat(animal.hunger_level) - parseFloat(animal.hunger_rate));

    let newState = animal.current_state;
    if (newHunger < 20) {
      newState = animal.species === 'Wolf' ? 'Hunting' : 'Foraging';
    } else if (newHunger < 40) {
      newState = 'Hungry';
    } else if (animal.current_state === 'Hungry' || animal.current_state === 'Foraging') {
      newState = 'Docile';
    }

    await db.query(
      'UPDATE animal_states SET hunger_level = $1, current_state = $2, updated_at = NOW() WHERE entity_id = $3',
      [newHunger, newState, animal.id]
    );

    if (newHunger <= 0) {
      const newHealth = Math.max(0, parseFloat(animal.hunger) - 5);
      if (newHealth <= 0) {
        await db.query(
          'UPDATE entities SET health = 0, is_dead = TRUE, updated_at = NOW() WHERE id = $1',
          [animal.id]
        );
      } else {
        await db.query(
          'UPDATE entities SET health = $1, updated_at = NOW() WHERE id = $2',
          [newHealth, animal.id]
        );
      }
    }
  }

  const npcs = await db.query(
    `SELECT e.id, e.hunger, n.current_state, n.has_adequate_resources, n.has_shelter,
            n.disgruntled_days, n.consecutive_food_days, n.consecutive_water_days, n.consecutive_shelter_days
     FROM entities e
     JOIN npc_states n ON e.id = n.entity_id
     WHERE e.world_id = $1 AND e.is_dead = FALSE AND e.entity_type = 'NPC'`,
    [worldId]
  );

  for (const npc of npcs.rows) {
    let newDisgruntledDays = npc.disgruntled_days;
    let newState = npc.current_state;
    let newAction = 'Wait';

    if (!npc.has_adequate_resources) {
      newDisgruntledDays++;
      if (newDisgruntledDays >= 3) {
        newState = 'Threatened';
        newAction = 'Flee';
      }
    } else {
      newDisgruntledDays = 0;
      if (npc.has_shelter) {
        newState = 'HasFoodShelter';
        newAction = 'Socialize';
      }
    }

    const newFoodDays = npc.has_adequate_resources ? npc.consecutive_food_days + 1 : 0;
    const newWaterDays = npc.has_adequate_resources ? npc.consecutive_water_days + 1 : 0;
    const newShelterDays = npc.has_shelter ? npc.consecutive_shelter_days + 1 : 0;

    await db.query(
      `UPDATE npc_states SET current_state = $1, current_action = $2, disgruntled_days = $3,
              consecutive_food_days = $4, consecutive_water_days = $5, consecutive_shelter_days = $6, updated_at = NOW()
       WHERE entity_id = $7`,
      [newState, newAction, newDisgruntledDays, newFoodDays, newWaterDays, newShelterDays, npc.id]
    );
  }
}

async function processSourceLogicTick(worldId: string): Promise<void> {
  await db.query(
    `UPDATE tile_states SET
       days_since_last_check = days_since_last_check + 1,
       current_moisture = LEAST(100, current_moisture +
         CASE WHEN (SELECT weather_condition FROM worlds WHERE id = $1) IN ('Rain', 'Storm1', 'Storm2', 'Storm3')
              THEN 5 ELSE -1 END),
       sun_accumulation = sun_accumulation +
         CASE WHEN (SELECT weather_condition FROM worlds WHERE id = $1) IN ('Sunny', 'Clear')
              THEN 1 ELSE 0 END
     WHERE world_id = $1`,
    [worldId]
  );

  await db.query(
    `UPDATE tile_states SET fire_state = 'None'
     WHERE world_id = $1 AND fire_state = 'Burning'
       AND (SELECT weather_condition FROM worlds WHERE id = $1) IN ('Rain', 'Snow', 'Storm1', 'Storm2', 'Storm3')`,
    [worldId]
  );

  await db.query(
    `UPDATE tile_states SET
       current_temperature = current_temperature +
         CASE
           WHEN (SELECT weather_condition FROM worlds WHERE id = $1) IN ('Sunny') THEN 2
           WHEN (SELECT weather_condition FROM worlds WHERE id = $1) IN ('Snow', 'Storm3') THEN -3
           WHEN (SELECT weather_condition FROM worlds WHERE id = $1) IN ('Rain') THEN -1
           ELSE 0
         END
     WHERE world_id = $1`,
    [worldId]
  );

  const stormWeather = await db.query(
    "SELECT weather_condition FROM worlds WHERE id = $1 AND weather_condition IN ('Storm1', 'Storm2', 'Storm3')",
    [worldId]
  );

  if (stormWeather.rows.length > 0) {
    await db.query(
      `UPDATE tile_states SET erosion_damage = erosion_damage + 1
       WHERE world_id = $1 AND current_moisture > 50`,
      [worldId]
    );
  }
}

async function processPassiveAP(
  worldId: string,
  accountId: string
): Promise<{ granted: number; newBalance: number }> {
  const apRow = await db.query(
    'SELECT balance, last_passive_ap_time FROM ap_balances WHERE world_id = $1 AND account_id = $2 FOR UPDATE',
    [worldId, accountId]
  );

  if (apRow.rows.length === 0) {
    return { granted: 0, newBalance: 0 };
  }

  const { balance, last_passive_ap_time } = apRow.rows[0];
  const lastAP = new Date(last_passive_ap_time);
  const now = new Date();
  const hoursSince = (now.getTime() - lastAP.getTime()) / (1000 * 60 * 60);

  if (hoursSince < config.ap.passiveIntervalHours) {
    return { granted: 0, newBalance: balance };
  }

  const intervalsElapsed = Math.floor(hoursSince / config.ap.passiveIntervalHours);
  const grantAmount = intervalsElapsed * config.ap.dailyPassiveAmount;
  const newBalance = balance + grantAmount;

  await db.query(
    'UPDATE ap_balances SET balance = $1, last_passive_ap_time = NOW(), updated_at = NOW() WHERE world_id = $2 AND account_id = $3',
    [newBalance, worldId, accountId]
  );

  await db.query(
    `INSERT INTO architect_point_ledger (world_id, account_id, entry_type, amount, balance_after, reason)
     VALUES ($1, $2, 'passive_grant', $3, $4, $5)`,
    [worldId, accountId, grantAmount, newBalance, `Passive AP: ${intervalsElapsed} intervals`]
  );

  return { granted: grantAmount, newBalance };
}

async function processOfflineCatchup(
  worldId: string,
  accountId: string,
  offlineHours: number
): Promise<{ daysAdvanced: number; eventsProcessed: number }> {
  const capped = Math.min(offlineHours, 168);
  const simDays = Math.floor(capped / 24);

  if (simDays <= 0) {
    return { daysAdvanced: 0, eventsProcessed: 0 };
  }

  const world = await db.query(
    'SELECT day_count, current_season FROM worlds WHERE id = $1',
    [worldId]
  );

  const oldDay = world.rows[0]?.day_count || 1;
  const newDay = oldDay + simDays;
  const newSeason = Math.floor((newDay / 30) % 4);

  await db.query(
    'UPDATE worlds SET day_count = $1, current_season = $2, updated_at = NOW() WHERE id = $3',
    [newDay, newSeason, worldId]
  );

  const soulform = await db.query(
    'SELECT hunger, health, comfort FROM soulform_states WHERE world_id = $1',
    [worldId]
  );

  if (soulform.rows.length > 0) {
    const sf = soulform.rows[0];
    const newHunger = Math.max(0, sf.hunger - simDays * 5);
    const newComfort = Math.max(0, sf.comfort - simDays * 2);
    const healthDecay = newHunger < 20 ? simDays * 2 : 0;
    const newHealth = Math.max(0, sf.health - healthDecay);

    await db.query(
      'UPDATE soulform_states SET hunger = $1, comfort = $2, health = $3, updated_at = NOW() WHERE world_id = $4',
      [newHunger, newComfort, newHealth, worldId]
    );
  }

  return { daysAdvanced: simDays, eventsProcessed: simDays };
}

async function processSoulformTick(worldId: string): Promise<void> {
  const sf = await db.query(
    'SELECT mood, hunger, comfort, curiosity, current_activity FROM soulform_states WHERE world_id = $1',
    [worldId]
  );

  if (sf.rows.length === 0) return;

  const s = sf.rows[0];
  let newMood = s.mood;

  if (s.hunger < 20) {
    newMood = 'Hungry';
  } else if (s.comfort < 20) {
    newMood = 'Distressed';
  } else if (s.curiosity > 70) {
    newMood = 'Curious';
  } else if (s.comfort > 70) {
    newMood = 'Content';
  }

  if (newMood !== s.mood) {
    await db.query(
      'UPDATE soulform_states SET mood = $1, updated_at = NOW() WHERE world_id = $2',
      [newMood, worldId]
    );
  }
}

export async function getAPBalance(worldId: string, accountId: string): Promise<{
  balance: number;
  lastPassiveAPTime: string;
}> {
  const result = await db.query(
    'SELECT balance, last_passive_ap_time FROM ap_balances WHERE world_id = $1 AND account_id = $2',
    [worldId, accountId]
  );

  if (result.rows.length === 0) {
    return { balance: config.ap.startingBalance, lastPassiveAPTime: new Date().toISOString() };
  }

  return {
    balance: result.rows[0].balance,
    lastPassiveAPTime: result.rows[0].last_passive_ap_time.toISOString(),
  };
}

export async function spendAP(
  worldId: string,
  accountId: string,
  amount: number,
  reason: string
): Promise<{ newBalance: number }> {
  const apRow = await db.query(
    'SELECT balance FROM ap_balances WHERE world_id = $1 AND account_id = $2 FOR UPDATE',
    [worldId, accountId]
  );

  const currentBalance = apRow.rows[0]?.balance || 0;
  if (currentBalance < amount) {
    throw new AppError(400, `Insufficient AP. Required: ${amount}, Balance: ${currentBalance}`);
  }

  const newBalance = currentBalance - amount;
  await db.query(
    'UPDATE ap_balances SET balance = $1, updated_at = NOW() WHERE world_id = $2 AND account_id = $3',
    [newBalance, worldId, accountId]
  );

  await db.query(
    `INSERT INTO architect_point_ledger (world_id, account_id, entry_type, amount, balance_after, reason)
     VALUES ($1, $2, 'intervention_spend', $3, $4, $5)`,
    [worldId, accountId, -amount, newBalance, reason]
  );

  return { newBalance };
}

export async function grantAP(
  worldId: string,
  accountId: string,
  amount: number,
  entryType: string,
  reason: string
): Promise<{ newBalance: number }> {
  const apRow = await db.query(
    'SELECT balance FROM ap_balances WHERE world_id = $1 AND account_id = $2 FOR UPDATE',
    [worldId, accountId]
  );

  const currentBalance = apRow.rows[0]?.balance || 0;
  const newBalance = currentBalance + amount;

  await db.query(
    'UPDATE ap_balances SET balance = $1, updated_at = NOW() WHERE world_id = $2 AND account_id = $3',
    [newBalance, worldId, accountId]
  );

  await db.query(
    `INSERT INTO architect_point_ledger (world_id, account_id, entry_type, amount, balance_after, reason)
     VALUES ($1, $2, $3, $4, $5, $6)`,
    [worldId, accountId, entryType, amount, newBalance, reason]
  );

  return { newBalance };
}

export function compressState(state: unknown): Buffer {
  const json = JSON.stringify(state);
  return zlib.gzipSync(Buffer.from(json, 'utf-8'));
}

export function decompressState(data: Buffer): unknown {
  return JSON.parse(zlib.gunzipSync(data).toString('utf-8'));
}
