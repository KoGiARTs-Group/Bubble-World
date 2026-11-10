import { Router } from 'express';
import * as entityService from '../services/entityService';
import { requireAuth } from '../middleware/auth';
import { requireWorldAccess } from '../middleware/worldAccess';
import { asyncHandler } from '../middleware/errorHandler';
import { AuthenticatedRequest, param, queryParam } from '../types';

const router = Router();

router.get(
  '/:worldId/entities',
  requireAuth,
  requireWorldAccess('viewer'),
  asyncHandler(async (req: AuthenticatedRequest, res) => {
    const worldId = param(req, 'worldId');
    const entityType = queryParam(req, 'type');
    const entities = await entityService.getEntities(worldId, entityType);
    res.json({ status: 'success', data: { entities } });
  })
);

router.get(
  '/:worldId/entities/:entityId',
  requireAuth,
  requireWorldAccess('viewer'),
  asyncHandler(async (req: AuthenticatedRequest, res) => {
    const worldId = param(req, 'worldId');
    const entityId = param(req, 'entityId');
    const entity = await entityService.getEntity(worldId, entityId);
    res.json({ status: 'success', data: { entity } });
  })
);

router.post(
  '/:worldId/entities',
  requireAuth,
  requireWorldAccess('member'),
  asyncHandler(async (req: AuthenticatedRequest, res) => {
    const worldId = param(req, 'worldId');
    const { entityType, gridX, gridY, gridZ, facing, health, hunger, npcState, animalState, soulformEntityState } = req.body;

    if (!entityType || !['NPC', 'Animal', 'Soulform'].includes(entityType)) {
      return res.status(400).json({ status: 'error', message: 'entityType must be NPC, Animal, or Soulform' });
    }

    const entity = await entityService.spawnEntity(worldId, entityType, {
      gridX, gridY, gridZ, facing, health, hunger, npcState, animalState, soulformEntityState,
    });
    res.status(201).json({ status: 'success', data: { entity } });
  })
);

router.patch(
  '/:worldId/entities/:entityId',
  requireAuth,
  requireWorldAccess('member'),
  asyncHandler(async (req: AuthenticatedRequest, res) => {
    const worldId = param(req, 'worldId');
    const entityId = param(req, 'entityId');
    const entity = await entityService.updateEntity(worldId, entityId, req.body);
    res.json({ status: 'success', data: { entity } });
  })
);

router.delete(
  '/:worldId/entities/:entityId',
  requireAuth,
  requireWorldAccess('owner'),
  asyncHandler(async (req: AuthenticatedRequest, res) => {
    const worldId = param(req, 'worldId');
    const entityId = param(req, 'entityId');
    await entityService.removeEntity(worldId, entityId);
    res.json({ status: 'success', message: 'Entity removed' });
  })
);

router.get(
  '/:worldId/bubbles',
  requireAuth,
  requireWorldAccess('viewer'),
  asyncHandler(async (req: AuthenticatedRequest, res) => {
    const worldId = param(req, 'worldId');
    const bubbles = await entityService.getBubbles(worldId);
    res.json({ status: 'success', data: { bubbles } });
  })
);

router.post(
  '/:worldId/bubbles',
  requireAuth,
  requireWorldAccess('owner'),
  asyncHandler(async (req: AuthenticatedRequest, res) => {
    const worldId = param(req, 'worldId');
    const { bubbleId, environmentType, gridWidth, gridHeight, gridDepth, seasonOffset } = req.body;

    if (!bubbleId) {
      return res.status(400).json({ status: 'error', message: 'bubbleId is required' });
    }

    const bubble = await entityService.createBubble(
      worldId, bubbleId, environmentType || 'MeadowGrassland',
      gridWidth || 60, gridHeight || 60, gridDepth || 8, seasonOffset || 0
    );
    res.status(201).json({ status: 'success', data: { bubble } });
  })
);

router.get(
  '/:worldId/tiles/:bubbleId',
  requireAuth,
  requireWorldAccess('viewer'),
  asyncHandler(async (req: AuthenticatedRequest, res) => {
    const worldId = param(req, 'worldId');
    const bubbleId = param(req, 'bubbleId');
    const cells = await entityService.getTileCells(worldId, bubbleId);
    res.json({ status: 'success', data: { cells } });
  })
);

router.put(
  '/:worldId/tiles/:bubbleId/:x/:y/:z',
  requireAuth,
  requireWorldAccess('member'),
  asyncHandler(async (req: AuthenticatedRequest, res) => {
    const worldId = param(req, 'worldId');
    const bubbleId = param(req, 'bubbleId');
    const x = parseInt(param(req, 'x'), 10);
    const y = parseInt(param(req, 'y'), 10);
    const z = parseInt(param(req, 'z'), 10);

    if (isNaN(x) || isNaN(y) || isNaN(z)) {
      return res.status(400).json({ status: 'error', message: 'Invalid tile coordinates' });
    }

    await entityService.setTileCell(worldId, bubbleId, x, y, z, req.body);
    res.json({ status: 'success', message: 'Tile updated' });
  })
);

router.delete(
  '/:worldId/tiles/:bubbleId/:x/:y/:z',
  requireAuth,
  requireWorldAccess('member'),
  asyncHandler(async (req: AuthenticatedRequest, res) => {
    const worldId = param(req, 'worldId');
    const bubbleId = param(req, 'bubbleId');
    const x = parseInt(param(req, 'x'), 10);
    const y = parseInt(param(req, 'y'), 10);
    const z = parseInt(param(req, 'z'), 10);

    if (isNaN(x) || isNaN(y) || isNaN(z)) {
      return res.status(400).json({ status: 'error', message: 'Invalid tile coordinates' });
    }

    const removed = await entityService.removeTileCell(worldId, bubbleId, x, y, z);
    res.json({ status: 'success', removed });
  })
);

export default router;
