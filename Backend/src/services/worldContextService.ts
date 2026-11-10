import { db } from '../db';

/**
 * Builds the worldContext string the Soulform brain prompt expects,
 * using server-authoritative data from the database.
 *
 * Format matches what Unity's BuildSystemMessage extracts:
 *   Name: / Temperament: / Weather: / Food: / Nearby: / Hunger: / Comfort: / Curiosity: / Memories:
 */
export async function buildWorldContext(worldId: string): Promise<string> {
  const world = await db.query(
    `SELECT name, environment_type, weather_condition, day_count, current_season, livability_percent
     FROM worlds WHERE id = $1`,
    [worldId]
  );

  if (world.rows.length === 0) {
    return 'Weather: Clear\nFood: unknown\nNearby: open ground\nHunger: 50\nComfort: 50\nCuriosity: 50';
  }

  const w = world.rows[0];

  const soulform = await db.query(
    `SELECT name, temperament, mood, grid_x, grid_y, grid_z, comfort, hunger, curiosity,
            current_activity, health
     FROM soulform_states WHERE world_id = $1`,
    [worldId]
  );
  const sf = soulform.rows[0] || {};

  const seasonNames = ['Spring', 'Summer', 'Autumn', 'Winter'];
  const season = seasonNames[w.current_season] || 'Spring';

  const food = describeFoodSupply(w.livability_percent || 0);
  const nearby = describeNearby(worldId, w.environment_type);

  const memories = await buildMemoryContext(worldId);

  const lines = [
    `Name: ${sf.name || 'Soulform'}`,
    `Temperament: ${sf.temperament || 'Inquisitive'}`,
    `Weather: ${w.weather_condition || 'Clear'}`,
    `Season: ${season} (Day ${w.day_count})`,
    `Food: ${food}`,
    `Nearby: ${nearby}`,
    `Hunger: ${sf.hunger ?? 50}`,
    `Comfort: ${sf.comfort ?? 50}`,
    `Curiosity: ${sf.curiosity ?? 50}`,
    `Health: ${sf.health ?? 100}`,
    `Mood: ${sf.mood || 'Content'}`,
    `Position: (${sf.grid_x ?? 12}, ${sf.grid_y ?? 0}, ${sf.grid_z ?? 12})`,
    `Livability: ${w.livability_percent || 0}%`,
    `Memories: ${memories}`,
  ];

  return lines.join('\n');
}

function describeFoodSupply(livability: number): string {
  if (livability >= 75) return 'abundant';
  if (livability >= 50) return 'adequate';
  if (livability >= 25) return 'scarce';
  return 'very scarce';
}

async function describeNearby(worldId: string, environment: string): Promise<string> {
  const entityCount = await db.query(
    `SELECT entity_type, COUNT(*) as count FROM entities
     WHERE world_id = $1 AND is_dead = FALSE
     GROUP BY entity_type`,
    [worldId]
  );

  const parts: string[] = [];
  for (const row of entityCount.rows) {
    const count = parseInt(row.count, 10);
    if (count > 0) {
      parts.push(`${count} ${row.entity_type.toLowerCase()}${count > 1 ? 's' : ''}`);
    }
  }

  const envLabel = environment === 'Forest' ? 'trees'
    : environment === 'BeachCoastal' ? 'shoreline'
    : 'open ground';

  if (parts.length === 0) return envLabel;
  return `${envLabel}, ${parts.join(', ')}`;
}

async function buildMemoryContext(worldId: string): Promise<string> {
  const entries = await db.query(
    `SELECT me.description, me.category, mc.chapter_index
     FROM memory_entries me
     JOIN memory_chapters mc ON me.chapter_id = mc.id
     WHERE mc.world_id = $1
     ORDER BY me.created_at DESC
     LIMIT 5`,
    [worldId]
  );

  if (entries.rows.length === 0) {
    return 'No significant memories yet.';
  }

  return entries.rows
    .map((e: { description: string; category: string }) => `- [${e.category}] ${e.description}`)
    .join('\n');
}
