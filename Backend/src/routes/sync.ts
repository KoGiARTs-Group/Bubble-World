import { Router } from 'express';
import { z } from 'zod';
import { requireSharedKey } from '../middleware/sharedKey';
import { validate } from '../middleware/validation';
import { asyncHandler } from '../middleware/errorHandler';
import * as worldSyncService from '../services/worldSyncService';

const router = Router();

// World-state sync for the Unity client (no login — shared x-api-key instead,
// same rule as the brain routes). The snapshot `state` is an opaque string:
// the client owns its shape (BubbleWorldSaveData JSON), the backend owns
// versioning and durability.

const putSchema = z.object({
  worldKey: z.string().regex(/^[A-Za-z0-9_-]{1,64}$/).optional(),
  state: z.string().min(1).max(20_000_000),
});

// GET → version 0 / state null means "nothing stored yet" (client keeps or
// pushes its local state).
router.get(
  '/state',
  requireSharedKey,
  asyncHandler(async (req, res) => {
    const worldKey = typeof req.query.worldKey === 'string' ? req.query.worldKey : 'default';
    if (!/^[A-Za-z0-9_-]{1,64}$/.test(worldKey)) {
      res.status(400).json({ status: 'error', message: 'Invalid worldKey' });
      return;
    }

    const snap = await worldSyncService.getState(worldKey);
    res.json({ status: 'success', data: snap });
  })
);

router.put(
  '/state',
  requireSharedKey,
  validate(putSchema),
  asyncHandler(async (req, res) => {
    const { worldKey = 'default', state } = req.body;

    // Reject corrupt blobs here so the stored version is always loadable JSON.
    try {
      JSON.parse(state);
    } catch {
      res.status(400).json({ status: 'error', message: 'state must be valid JSON' });
      return;
    }

    const snap = await worldSyncService.putState(worldKey, state);
    res.json({ status: 'success', data: snap });
  })
);

export default router;
