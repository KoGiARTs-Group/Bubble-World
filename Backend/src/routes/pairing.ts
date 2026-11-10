import { Router } from 'express';
import * as pairingService from '../services/pairingService';
import { requireAuth } from '../middleware/auth';
import { requireSharedKey } from '../middleware/sharedKey';
import { validate, schemas } from '../middleware/validation';
import { pairingLimiter } from '../middleware/rateLimiter';
import { asyncHandler } from '../middleware/errorHandler';
import { AuthenticatedRequest, param, queryParam } from '../types';

const router = Router();

router.post(
  '/session',
  requireAuth,
  pairingLimiter,
  validate(schemas.createPairing),
  asyncHandler(async (req: AuthenticatedRequest, res) => {
    const { worldId } = req.body;
    const result = await pairingService.createPairingSession(
      req.accountId!, worldId, req.deviceId
    );
    res.status(201).json({ status: 'success', data: result });
  })
);

router.post(
  '/claim',
  requireAuth,
  pairingLimiter,
  validate(schemas.claimPairing),
  asyncHandler(async (req: AuthenticatedRequest, res) => {
    const { shortCode } = req.body;
    const result = await pairingService.claimPairingSession(
      req.accountId!, shortCode, req.deviceId
    );
    res.json({ status: 'success', data: result });
  })
);

router.delete(
  '/session/:id',
  requireAuth,
  asyncHandler(async (req: AuthenticatedRequest, res) => {
    const id = param(req, 'id');
    await pairingService.cancelPairingSession(req.accountId!, id);
    res.json({ status: 'success', message: 'Pairing session cancelled' });
  })
);

// ---- Device-side sessions (Unity client: shared x-api-key, no login) ----

router.post(
  '/device/session',
  requireSharedKey,
  pairingLimiter,
  validate(schemas.createDevicePairing),
  asyncHandler(async (req, res) => {
    const worldKey = req.body.worldKey || 'default';
    const result = await pairingService.createDevicePairingSession(worldKey);
    res.status(201).json({ status: 'success', data: result });
  })
);

router.delete(
  '/device/session/:id',
  requireSharedKey,
  asyncHandler(async (req, res) => {
    const id = param(req, 'id');
    if (!/^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i.test(id)) {
      res.status(400).json({ status: 'error', message: 'Invalid session id' });
      return;
    }
    const worldKey = queryParam(req, 'worldKey') || 'default';
    await pairingService.cancelDevicePairingSession(id, worldKey);
    res.json({ status: 'success', message: 'Pairing session cancelled' });
  })
);

// Worlds the calling account has linked by claiming device sessions —
// how the companion app discovers what it can read after /claim.
router.get(
  '/linked',
  requireAuth,
  asyncHandler(async (req: AuthenticatedRequest, res) => {
    const worldKeys = await pairingService.listLinkedWorldKeys(req.accountId!);
    res.json({ status: 'success', data: { worldKeys } });
  })
);

export default router;
