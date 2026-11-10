import crypto from 'crypto';
import type { Request, Response, NextFunction } from 'express';
import { config } from '../config';

// The Unity client sends the shared secret from SoulformBrainConfig.ApiKey as
// x-api-key on endpoints it calls without a login (brain chat, world sync).
// Enforced only when BRAIN_API_KEY is configured server-side so local dev
// without a key keeps working; set it in production to stop these from being
// an open proxy.
export function requireSharedKey(req: Request, res: Response, next: NextFunction): void {
  const expected = config.brain.apiKey;
  if (!expected) {
    next();
    return;
  }

  const provided = req.header('x-api-key') || '';
  const providedBuf = Buffer.from(provided, 'utf8');
  const expectedBuf = Buffer.from(expected, 'utf8');

  const valid =
    providedBuf.length === expectedBuf.length &&
    crypto.timingSafeEqual(providedBuf, expectedBuf);

  if (!valid) {
    res.status(401).json({ status: 'error', message: 'Invalid or missing x-api-key' });
    return;
  }
  next();
}
