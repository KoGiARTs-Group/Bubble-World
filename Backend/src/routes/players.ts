import { Router } from 'express';
import * as playerService from '../services/playerService';
import { requireAuth } from '../middleware/auth';
import { validate, schemas } from '../middleware/validation';
import { asyncHandler } from '../middleware/errorHandler';
import { AuthenticatedRequest } from '../types';

const router = Router();

router.get(
  '/me',
  requireAuth,
  asyncHandler(async (req: AuthenticatedRequest, res) => {
    const profile = await playerService.getPlayerProfile(req.accountId!);
    res.json({ status: 'success', data: profile });
  })
);

router.patch(
  '/me',
  requireAuth,
  validate(schemas.updateProfile),
  asyncHandler(async (req: AuthenticatedRequest, res) => {
    const profile = await playerService.updateProfile(req.accountId!, req.body);
    res.json({ status: 'success', data: profile });
  })
);

router.post(
  '/devices',
  requireAuth,
  validate(schemas.registerDevice),
  asyncHandler(async (req: AuthenticatedRequest, res) => {
    const { deviceType, deviceId, deviceName } = req.body;
    const device = await playerService.registerDevice(
      req.accountId!, deviceType, deviceId, deviceName
    );
    res.status(201).json({ status: 'success', data: device });
  })
);

export default router;
