import { v4 as uuid } from 'uuid';
import crypto from 'crypto';
import { db } from '../db';
import { config } from '../config';
import { AppError } from '../middleware/errorHandler';
import { PairingDisplayData } from '../types';

export async function createPairingSession(
  accountId: string,
  worldId: string,
  deviceId?: string
): Promise<PairingDisplayData> {
  const world = await db.query(
    'SELECT id, owner_account_id FROM worlds WHERE id = $1',
    [worldId]
  );

  if (world.rows.length === 0) {
    throw new AppError(404, 'World not found');
  }

  if (world.rows[0].owner_account_id !== accountId) {
    const member = await db.query(
      'SELECT role FROM world_members WHERE world_id = $1 AND account_id = $2',
      [worldId, accountId]
    );
    if (member.rows.length === 0) {
      throw new AppError(403, 'Not authorized for this world');
    }
  }

  const token = crypto.randomBytes(32).toString('hex');
  const tokenHash = crypto.createHash('sha256').update(token).digest('hex');
  const shortCode = generateShortCode();
  const sessionId = uuid();
  const expiresAt = new Date(Date.now() + config.pairing.codeExpiryMinutes * 60 * 1000);

  await db.query(
    `INSERT INTO pairing_sessions
     (id, account_id, world_id, device_id, token_hash, short_code, expires_at)
     VALUES ($1, $2, $3, $4, $5, $6, $7)`,
    [sessionId, accountId, worldId, deviceId || null, tokenHash, shortCode, expiresAt]
  );

  return {
    sessionId,
    shortCode,
    deepLink: `bubbleworld://pair?code=${shortCode}`,
    expiresAt: expiresAt.toISOString(),
  };
}

export async function claimPairingSession(
  claimingAccountId: string,
  shortCode: string,
  claimingDeviceId?: string
): Promise<{ success: boolean; worldId: string | null; worldKey: string | null; role: string }> {
  const pairing = await db.query(
    `SELECT id, account_id, world_id, world_key, status, expires_at
     FROM pairing_sessions
     WHERE short_code = $1 AND status = 'pending'`,
    [shortCode]
  );

  if (pairing.rows.length === 0) {
    throw new AppError(404, 'Invalid or expired pairing code');
  }

  const session = pairing.rows[0];

  if (new Date(session.expires_at) < new Date()) {
    await db.query(
      "UPDATE pairing_sessions SET status = 'expired' WHERE id = $1",
      [session.id]
    );
    throw new AppError(410, 'Pairing code has expired');
  }

  // Device sessions have no owner account — anyone may claim those.
  if (session.account_id != null && session.account_id === claimingAccountId) {
    throw new AppError(400, 'Cannot claim your own pairing session');
  }

  const client = await db.getClient();
  try {
    await client.query('BEGIN');

    const updated = await client.query(
      `UPDATE pairing_sessions
       SET status = 'claimed', claimed_by_account_id = $1,
           claimed_by_device_id = $2, claimed_at = NOW()
       WHERE id = $3 AND status = 'pending'
       RETURNING world_id, world_key`,
      [claimingAccountId, claimingDeviceId || null, session.id]
    );

    if (updated.rows.length === 0) {
      throw new AppError(409, 'Pairing code was already claimed');
    }

    const worldId = updated.rows[0].world_id;
    const worldKey = updated.rows[0].world_key;

    // world_members rows only exist for worlds-table worlds; a worldKey
    // session is linked through the pairing row itself (world_key +
    // claimed_by_account_id) and is read back via GET /pairing/linked.
    if (worldId != null) {
      const existingMember = await client.query(
        'SELECT id FROM world_members WHERE world_id = $1 AND account_id = $2',
        [worldId, claimingAccountId]
      );

      if (existingMember.rows.length === 0) {
        await client.query(
          `INSERT INTO world_members (world_id, account_id, role)
           VALUES ($1, $2, 'viewer')`,
          [worldId, claimingAccountId]
        );
      }
    }

    await client.query('COMMIT');

    return {
      success: true,
      worldId: worldId ?? null,
      worldKey: worldKey ?? null,
      role: 'viewer',
    };
  } catch (err) {
    await client.query('ROLLBACK');
    throw err;
  } finally {
    client.release();
  }
}

// Device-side session: created by the Unity client itself (shared x-api-key,
// no login). Bound to the client's worldKey instead of an account/world UUID.
export async function createDevicePairingSession(
  worldKey: string
): Promise<PairingDisplayData> {
  const token = crypto.randomBytes(32).toString('hex');
  const tokenHash = crypto.createHash('sha256').update(token).digest('hex');
  const shortCode = generateShortCode();
  const sessionId = uuid();
  const expiresAt = new Date(Date.now() + config.pairing.codeExpiryMinutes * 60 * 1000);

  await db.query(
    `INSERT INTO pairing_sessions
     (id, token_hash, short_code, expires_at, world_key)
     VALUES ($1, $2, $3, $4, $5)`,
    [sessionId, tokenHash, shortCode, expiresAt, worldKey]
  );

  return {
    sessionId,
    shortCode,
    deepLink: `bubbleworld://pair?code=${shortCode}`,
    expiresAt: expiresAt.toISOString(),
  };
}

export async function cancelDevicePairingSession(
  sessionId: string,
  worldKey: string
): Promise<void> {
  const result = await db.query(
    `UPDATE pairing_sessions SET status = 'cancelled'
     WHERE id = $1 AND world_key = $2 AND status = 'pending'`,
    [sessionId, worldKey]
  );

  if (result.rowCount === 0) {
    throw new AppError(404, 'Pairing session not found or already used');
  }
}

// Worlds a claiming (mobile) account can reach through device sessions.
export async function listLinkedWorldKeys(accountId: string): Promise<string[]> {
  const result = await db.query(
    `SELECT DISTINCT world_key FROM pairing_sessions
     WHERE claimed_by_account_id = $1 AND status = 'claimed' AND world_key IS NOT NULL
     ORDER BY world_key`,
    [accountId]
  );
  return result.rows.map((r) => r.world_key as string);
}

export async function cancelPairingSession(accountId: string, sessionId: string): Promise<void> {
  const result = await db.query(
    `UPDATE pairing_sessions SET status = 'cancelled'
     WHERE id = $1 AND account_id = $2 AND status = 'pending'`,
    [sessionId, accountId]
  );

  if (result.rowCount === 0) {
    throw new AppError(404, 'Pairing session not found or already used');
  }
}

export async function cleanupExpiredPairings(): Promise<number> {
  const result = await db.query(
    `UPDATE pairing_sessions SET status = 'expired'
     WHERE status = 'pending' AND expires_at < NOW()`
  );
  return result.rowCount || 0;
}

function generateShortCode(): string {
  const chars = 'ABCDEFGHJKLMNPQRSTUVWXYZ23456789';
  let code = '';
  for (let i = 0; i < 6; i++) {
    code += chars.charAt(Math.floor(Math.random() * chars.length));
  }
  return code;
}
