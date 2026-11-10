import { v4 as uuid } from 'uuid';
import { db } from '../db';
import { AuthenticatedRequest } from '../types';
import { AppError } from '../middleware/errorHandler';

export async function getPlayerProfile(accountId: string) {
  const account = await db.query(
    'SELECT id, email, display_name, created_at FROM accounts WHERE id = $1',
    [accountId]
  );

  if (account.rows.length === 0) {
    throw new AppError(404, 'Account not found');
  }

  const profile = await db.query(
    'SELECT display_name, region, preferences, consent_flags FROM player_profiles WHERE account_id = $1',
    [accountId]
  );

  const stats = await db.query(
    `SELECT total_play_time_seconds, total_days_played, worlds_created, total_population,
            skills_unlocked, skills_mastered, interventions_used, chat_messages_sent, offline_syncs
     FROM player_stats WHERE account_id = $1`,
    [accountId]
  );

  const platforms = await db.query(
    'SELECT platform, platform_user_id, created_at FROM platform_identities WHERE account_id = $1',
    [accountId]
  );

  const devices = await db.query(
    'SELECT id, device_type, device_id, device_name, is_trusted, last_seen_at FROM devices WHERE account_id = $1 ORDER BY last_seen_at DESC',
    [accountId]
  );

  return {
    account: {
      id: account.rows[0].id,
      email: account.rows[0].email,
      displayName: account.rows[0].display_name,
      createdAt: account.rows[0].created_at.toISOString(),
    },
    profile: profile.rows[0] || {},
    stats: stats.rows[0] || {},
    platforms: platforms.rows,
    devices: devices.rows.map((d: { id: string; device_type: string; device_id: string; device_name: string | null; is_trusted: boolean; last_seen_at: Date }) => ({
      id: d.id,
      type: d.device_type,
      name: d.device_name,
      isTrusted: d.is_trusted,
      lastSeenAt: d.last_seen_at.toISOString(),
    })),
  };
}

export async function updateProfile(
  accountId: string,
  updates: { displayName?: string; region?: string; preferences?: Record<string, unknown>; consentFlags?: Record<string, boolean> }
) {
  const setClauses: string[] = ['updated_at = NOW()'];
  const params: unknown[] = [];
  let paramIndex = 1;

  if (updates.displayName !== undefined) {
    setClauses.push(`display_name = $${paramIndex++}`);
    params.push(updates.displayName);
    await db.query(
      'UPDATE accounts SET display_name = $1, updated_at = NOW() WHERE id = $2',
      [updates.displayName, accountId]
    );
  }

  if (updates.region !== undefined) {
    setClauses.push(`region = $${paramIndex++}`);
    params.push(updates.region);
  }

  if (updates.preferences !== undefined) {
    setClauses.push(`preferences = COALESCE(preferences, '{}') || $${paramIndex++}`);
    params.push(JSON.stringify(updates.preferences));
  }

  if (updates.consentFlags !== undefined) {
    setClauses.push(`consent_flags = COALESCE(consent_flags, '{}') || $${paramIndex++}`);
    params.push(JSON.stringify(updates.consentFlags));
  }

  if (setClauses.length > 1) {
    params.push(accountId);
    await db.query(
      `UPDATE player_profiles SET ${setClauses.join(', ')} WHERE account_id = $${paramIndex}`,
      params
    );
  }

  return getPlayerProfile(accountId);
}

export async function registerDevice(
  accountId: string,
  deviceType: string,
  deviceId: string,
  deviceName?: string
) {
  const result = await db.query(
    `INSERT INTO devices (account_id, device_type, device_id, device_name, last_seen_at)
     VALUES ($1, $2, $3, $4, NOW())
     ON CONFLICT (account_id, device_id)
     DO UPDATE SET last_seen_at = NOW(), device_name = COALESCE($4, devices.device_name)
     RETURNING id, device_type, device_id, device_name, is_trusted, last_seen_at`,
    [accountId, deviceType, deviceId, deviceName || null]
  );

  return {
    id: result.rows[0].id,
    type: result.rows[0].device_type,
    deviceId: result.rows[0].device_id,
    name: result.rows[0].device_name,
    isTrusted: result.rows[0].is_trusted,
    lastSeenAt: result.rows[0].last_seen_at.toISOString(),
  };
}

export async function recordSyncCheckpoint(
  worldId: string,
  accountId: string,
  deviceId: string | undefined,
  checkpoint: string,
  version: number
): Promise<void> {
  await db.query(
    `INSERT INTO sync_checkpoints (world_id, account_id, device_id, checkpoint, version)
     VALUES ($1, $2, $3, $4, $5)
     ON CONFLICT (world_id, account_id, device_id)
     DO UPDATE SET checkpoint = $4, version = $5, created_at = NOW()`,
    [worldId, accountId, deviceId || null, checkpoint, version]
  );

  await db.query(
    'UPDATE player_stats SET offline_syncs = offline_syncs + 1 WHERE account_id = $1',
    [accountId]
  );
}

export async function getSyncCheckpoint(
  worldId: string,
  accountId: string,
  deviceId?: string
): Promise<{ checkpoint: string; version: number } | null> {
  const result = await db.query(
    'SELECT checkpoint, version FROM sync_checkpoints WHERE world_id = $1 AND account_id = $2 AND (device_id = $3 OR $3 IS NULL)',
    [worldId, accountId, deviceId || null]
  );

  if (result.rows.length === 0) return null;
  return {
    checkpoint: result.rows[0].checkpoint,
    version: result.rows[0].version,
  };
}
