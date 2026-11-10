import bcrypt from 'bcryptjs';
import jwt from 'jsonwebtoken';
import { v4 as uuid } from 'uuid';
import { db } from '../db';
import { config } from '../config';
import { AppError } from '../middleware/errorHandler';

interface TokenPair {
  accessToken: string;
  refreshToken: string;
}

interface AccountRow {
  id: string;
  email: string | null;
  password_hash: string | null;
  display_name: string | null;
}

export async function register(displayName: string, email?: string, password?: string): Promise<{ account: AccountRow; tokens: TokenPair }> {
  if (!email && !password) {
    throw new AppError(400, 'Email and password are required for registration');
  }
  if (!email || !password) {
    throw new AppError(400, 'Both email and password are required');
  }

  const existing = await db.query(
    'SELECT id FROM accounts WHERE email = $1',
    [email.toLowerCase()]
  );
  if (existing.rows.length > 0) {
    throw new AppError(409, 'An account with this email already exists');
  }

  const passwordHash = await bcrypt.hash(password, config.bcrypt.rounds);
  const accountId = uuid();

  const result = await db.query(
    `INSERT INTO accounts (id, email, password_hash, display_name)
     VALUES ($1, $2, $3, $4)
     RETURNING id, email, display_name`,
    [accountId, email.toLowerCase(), passwordHash, displayName]
  );

  const account = result.rows[0];

  await db.query(
    `INSERT INTO player_profiles (account_id, display_name)
     VALUES ($1, $2)`,
    [account.id, displayName]
  );

  await db.query(
    `INSERT INTO player_stats (account_id)
     VALUES ($1)`,
    [account.id]
  );

  const tokens = await createSession(account.id);
  return { account, tokens };
}

export async function login(email: string, password: string): Promise<{ account: AccountRow; tokens: TokenPair }> {
  const result = await db.query(
    'SELECT id, email, password_hash, display_name FROM accounts WHERE email = $1',
    [email.toLowerCase()]
  );

  if (result.rows.length === 0) {
    throw new AppError(401, 'Invalid credentials');
  }

  const account = result.rows[0];
  if (!account.password_hash) {
    throw new AppError(401, 'This account uses platform authentication');
  }

  const valid = await bcrypt.compare(password, account.password_hash);
  if (!valid) {
    throw new AppError(401, 'Invalid credentials');
  }

  const tokens = await createSession(account.id);
  return { account, tokens };
}

export async function platformAuth(
  platform: string,
  platformUserId: string,
  displayName?: string
): Promise<{ account: AccountRow; tokens: TokenPair; created: boolean }> {
  const existing = await db.query(
    `SELECT a.id, a.email, a.display_name
     FROM accounts a
     JOIN platform_identities pi ON a.id = pi.account_id
     WHERE pi.platform = $1 AND pi.platform_user_id = $2`,
    [platform, platformUserId]
  );

  if (existing.rows.length > 0) {
    const account = existing.rows[0];
    const tokens = await createSession(account.id);
    return { account, tokens, created: false };
  }

  const accountId = uuid();
  const name = displayName || `${platform}_${platformUserId.substring(0, 8)}`;

  const accountResult = await db.query(
    `INSERT INTO accounts (id, display_name)
     VALUES ($1, $2)
     RETURNING id, email, display_name`,
    [accountId, name]
  );
  const account = accountResult.rows[0];

  await db.query(
    `INSERT INTO platform_identities (account_id, platform, platform_user_id)
     VALUES ($1, $2, $3)`,
    [account.id, platform, platformUserId]
  );

  await db.query(
    `INSERT INTO player_profiles (account_id, display_name)
     VALUES ($1, $2)`,
    [account.id, name]
  );

  await db.query(
    `INSERT INTO player_stats (account_id)
     VALUES ($1)`,
    [account.id]
  );

  const tokens = await createSession(account.id);
  return { account, tokens, created: true };
}

export async function refreshTokens(refreshToken: string): Promise<TokenPair> {
  let decoded: { accountId: string; sessionId: string };
  try {
    decoded = jwt.verify(refreshToken, config.jwt.secret) as typeof decoded;
  } catch {
    throw new AppError(401, 'Invalid or expired refresh token');
  }

  const session = await db.query(
    'SELECT id, account_id, revoked_at FROM sessions WHERE id = $1',
    [decoded.sessionId]
  );

  if (session.rows.length === 0 || session.rows[0].revoked_at) {
    throw new AppError(401, 'Session has been revoked');
  }

  await db.query(
    'UPDATE sessions SET revoked_at = NOW() WHERE id = $1',
    [decoded.sessionId]
  );

  return createSession(session.rows[0].account_id);
}

export async function logout(accountId: string, refreshToken?: string): Promise<void> {
  if (refreshToken) {
    try {
      const decoded = jwt.verify(refreshToken, config.jwt.secret) as { sessionId: string };
      await db.query(
        'UPDATE sessions SET revoked_at = NOW() WHERE id = $1 AND account_id = $2',
        [decoded.sessionId, accountId]
      );
    } catch {
      // token invalid, just revoke all sessions
    }
  }
  await db.query(
    'UPDATE sessions SET revoked_at = NOW() WHERE account_id = $1 AND revoked_at IS NULL',
    [accountId]
  );
}

async function createSession(accountId: string, deviceId?: string, ipAddress?: string): Promise<TokenPair> {
  const sessionId = uuid();
  const refreshToken = jwt.sign(
    { accountId, sessionId },
    config.jwt.secret,
    { expiresIn: config.jwt.refreshExpiry } as jwt.SignOptions
  );

  const refreshTokenHash = await bcrypt.hash(refreshToken, 4);

  await db.query(
    `INSERT INTO sessions (id, account_id, refresh_token_hash, device_id, ip_address, expires_at)
     VALUES ($1, $2, $3, $4, $5, NOW() + INTERVAL '7 days')`,
    [sessionId, accountId, refreshTokenHash, deviceId || null, ipAddress || null]
  );

  const accessToken = jwt.sign(
    { accountId, deviceId },
    config.jwt.secret,
    { expiresIn: config.jwt.accessExpiry } as jwt.SignOptions
  );

  return { accessToken, refreshToken };
}
