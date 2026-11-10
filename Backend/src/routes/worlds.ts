import { Router } from 'express';
import * as worldService from '../services/worldService';
import * as simulationService from '../services/simulationService';
import * as playerService from '../services/playerService';
import { requireAuth } from '../middleware/auth';
import { requireWorldAccess } from '../middleware/worldAccess';
import { validate, schemas } from '../middleware/validation';
import { actionLimiter } from '../middleware/rateLimiter';
import { asyncHandler } from '../middleware/errorHandler';
import { AuthenticatedRequest, param, queryParam } from '../types';

const router = Router();

router.get(
  '/',
  requireAuth,
  asyncHandler(async (req: AuthenticatedRequest, res) => {
    const worlds = await worldService.getWorldsForAccount(req.accountId!);
    res.json({ status: 'success', data: { worlds } });
  })
);

router.post(
  '/',
  requireAuth,
  validate(schemas.createWorld),
  asyncHandler(async (req: AuthenticatedRequest, res) => {
    const { name, environmentType, gridWidth, gridHeight, gridDepth } = req.body;
    const world = await worldService.createWorld(
      req.accountId!, name, environmentType, gridWidth, gridHeight, gridDepth
    );
    res.status(201).json({ status: 'success', data: { world } });
  })
);

router.get(
  '/:worldId/state',
  requireAuth,
  requireWorldAccess('viewer'),
  asyncHandler(async (req: AuthenticatedRequest, res) => {
    const worldId = param(req, 'worldId');
    const state = await worldService.getWorldState(req.accountId!, worldId);
    res.json({ status: 'success', data: { state } });
  })
);

router.put(
  '/:worldId/state',
  requireAuth,
  requireWorldAccess('owner'),
  validate(schemas.uploadWorldState),
  asyncHandler(async (req: AuthenticatedRequest, res) => {
    const worldId = param(req, 'worldId');
    const result = await worldService.uploadWorldState(req.accountId!, worldId, req.body);
    res.json({ status: 'success', data: result });
  })
);

router.get(
  '/:worldId/sync',
  requireAuth,
  requireWorldAccess('viewer'),
  asyncHandler(async (req: AuthenticatedRequest, res) => {
    const worldId = param(req, 'worldId');
    const since = queryParam(req, 'since');
    const result = await worldService.getSyncDiff(req.accountId!, worldId, since || '');

    if (result.type === 'full') {
      await playerService.recordSyncCheckpoint(
        worldId, req.accountId!, req.deviceId,
        result.checkpoint || '', result.version
      );
    }

    res.json({ status: 'success', data: result });
  })
);

router.post(
  '/:worldId/actions',
  requireAuth,
  requireWorldAccess('member'),
  actionLimiter,
  validate(schemas.submitActions),
  asyncHandler(async (req: AuthenticatedRequest, res) => {
    const worldId = param(req, 'worldId');
    const { actions } = req.body;
    const result = await worldService.submitActions(
      req.accountId!, worldId, actions
    );
    res.json({ status: 'success', data: result });
  })
);

router.post(
  '/:worldId/tick',
  requireAuth,
  requireWorldAccess('owner'),
  validate(schemas.tick),
  asyncHandler(async (req: AuthenticatedRequest, res) => {
    const worldId = param(req, 'worldId');
    const { tickType, offlineDurationHours } = req.body;
    const result = await simulationService.processTick(
      worldId, req.accountId!, tickType, offlineDurationHours
    );
    res.json({ status: 'success', data: result });
  })
);

router.get(
  '/:worldId/live',
  requireAuth,
  requireWorldAccess('viewer'),
  asyncHandler(async (req: AuthenticatedRequest, res) => {
    const worldId = param(req, 'worldId');
    const summary = await worldService.getLiveSummary(req.accountId!, worldId);
    res.json({ status: 'success', data: { live: summary } });
  })
);

router.post(
  '/:worldId/interventions',
  requireAuth,
  requireWorldAccess('member'),
  actionLimiter,
  asyncHandler(async (req: AuthenticatedRequest, res) => {
    const worldId = param(req, 'worldId');
    const { interventionType, apCost } = req.body;
    if (!interventionType) {
      throw new Error('Intervention type is required');
    }

    const cost = apCost || 30;
    const result = await simulationService.spendAP(
      worldId, req.accountId!, cost,
      `Intervention: ${interventionType}`
    );

    res.json({ status: 'success', data: { newBalance: result.newBalance, interventionApplied: true } });
  })
);

router.get(
  '/:worldId/ap',
  requireAuth,
  requireWorldAccess('viewer'),
  asyncHandler(async (req: AuthenticatedRequest, res) => {
    const worldId = param(req, 'worldId');
    const ap = await simulationService.getAPBalance(worldId, req.accountId!);
    res.json({ status: 'success', data: { ap } });
  })
);

export default router;
