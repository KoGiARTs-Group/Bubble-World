import { Router } from 'express';
import { requireAuth } from '../middleware/auth';
import { validate, schemas } from '../middleware/validation';
import { asyncHandler } from '../middleware/errorHandler';
import { AuthenticatedRequest, param, queryParam } from '../types';
import { db } from '../db';
import * as companionService from '../services/companionService';

const router = Router();

router.post(
  '/:worldId/soulform/brain',
  requireAuth,
  validate(schemas.soulformBrain),
  asyncHandler(async (req: AuthenticatedRequest, res) => {
    const worldId = param(req, 'worldId');
    const { mode, playerMessage } = req.body;

    const world = await db.query(
      'SELECT owner_account_id FROM worlds WHERE id = $1',
      [worldId]
    );
    if (world.rows.length === 0) {
      return res.status(404).json({ status: 'error', message: 'World not found' });
    }

    const isOwner = world.rows[0].owner_account_id === req.accountId;
    if (!isOwner) {
      const member = await db.query(
        'SELECT role FROM world_members WHERE world_id = $1 AND account_id = $2',
        [worldId, req.accountId]
      );
      if (member.rows.length === 0) {
        return res.status(403).json({ status: 'error', message: 'Not authorized' });
      }
      if (mode === 'chat' && member.rows[0].role === 'viewer') {
        return res.status(403).json({ status: 'error', message: 'Viewers cannot chat with the Soulform' });
      }
    }

    if (mode === 'chat' && !playerMessage) {
      return res.status(400).json({ status: 'error', message: 'playerMessage is required for chat mode' });
    }

    const result = await companionService.querySoulformBrain(worldId, mode, playerMessage);
    res.json({ status: 'success', data: result });
  })
);

router.get(
  '/:worldId/soulform',
  requireAuth,
  asyncHandler(async (req: AuthenticatedRequest, res) => {
    const worldId = param(req, 'worldId');

    const world = await db.query('SELECT owner_account_id FROM worlds WHERE id = $1', [worldId]);
    if (world.rows.length === 0) {
      return res.status(404).json({ status: 'error', message: 'World not found' });
    }

    if (world.rows[0].owner_account_id !== req.accountId) {
      const member = await db.query(
        'SELECT role FROM world_members WHERE world_id = $1 AND account_id = $2',
        [worldId, req.accountId]
      );
      if (member.rows.length === 0) {
        return res.status(403).json({ status: 'error', message: 'Not authorized' });
      }
    }

    const soulform = await db.query('SELECT * FROM soulform_states WHERE world_id = $1', [worldId]);
    if (soulform.rows.length === 0) {
      return res.status(404).json({ status: 'error', message: 'Soulform not found' });
    }

    res.json({ status: 'success', data: { soulform: soulform.rows[0] } });
  })
);

router.patch(
  '/:worldId/soulform',
  requireAuth,
  asyncHandler(async (req: AuthenticatedRequest, res) => {
    const worldId = param(req, 'worldId');

    const world = await db.query(
      'SELECT owner_account_id FROM worlds WHERE id = $1',
      [worldId]
    );
    if (world.rows.length === 0 || world.rows[0].owner_account_id !== req.accountId) {
      return res.status(403).json({ status: 'error', message: 'Not authorized' });
    }

    const allowedFields = [
      'name', 'meaning', 'nature', 'temperament', 'mood',
      'grid_x', 'grid_y', 'grid_z', 'comfort', 'hunger', 'curiosity',
      'health', 'current_activity', 'current_thought', 'current_expression',
      'current_animation', 'appearance', 'voice', 'outfit', 'bond_traits', 'skill_data'
    ];

    const setClauses: string[] = ['updated_at = NOW()'];
    const params: unknown[] = [];
    let paramIndex = 1;

    for (const [key, value] of Object.entries(req.body)) {
      if (allowedFields.includes(key)) {
        setClauses.push(`${key} = $${paramIndex++}`);
        params.push(typeof value === 'object' ? JSON.stringify(value) : value);
      }
    }

    if (setClauses.length > 1) {
      params.push(worldId);
      await db.query(
        `UPDATE soulform_states SET ${setClauses.join(', ')} WHERE world_id = $${paramIndex}`,
        params
      );
    }

    const soulform = await db.query('SELECT * FROM soulform_states WHERE world_id = $1', [worldId]);
    res.json({ status: 'success', data: { soulform: soulform.rows[0] } });
  })
);

router.get(
  '/:worldId/events',
  requireAuth,
  asyncHandler(async (req: AuthenticatedRequest, res) => {
    const worldId = param(req, 'worldId');
    const limit = parseInt(queryParam(req, 'limit') || '50', 10);
    const offset = parseInt(queryParam(req, 'offset') || '0', 10);

    const world = await db.query('SELECT owner_account_id FROM worlds WHERE id = $1', [worldId]);
    if (world.rows.length === 0) {
      return res.status(404).json({ status: 'error', message: 'World not found' });
    }

    if (world.rows[0].owner_account_id !== req.accountId) {
      const member = await db.query(
        'SELECT role FROM world_members WHERE world_id = $1 AND account_id = $2',
        [worldId, req.accountId]
      );
      if (member.rows.length === 0) {
        return res.status(403).json({ status: 'error', message: 'Not authorized' });
      }
    }

    const events = await db.query(
      `SELECT id, event_type, description, source, event_data, server_timestamp
       FROM world_event_log
       WHERE world_id = $1
       ORDER BY server_timestamp DESC
       LIMIT $2 OFFSET $3`,
      [worldId, limit, offset]
    );

    res.json({ status: 'success', data: { events: events.rows } });
  })
);

router.post(
  '/:worldId/events',
  requireAuth,
  asyncHandler(async (req: AuthenticatedRequest, res) => {
    const worldId = param(req, 'worldId');
    const { eventType, description, source, eventData } = req.body;

    if (!eventType || !description) {
      return res.status(400).json({ status: 'error', message: 'eventType and description required' });
    }

    const world = await db.query(
      'SELECT owner_account_id FROM worlds WHERE id = $1',
      [worldId]
    );
    if (world.rows.length === 0 || world.rows[0].owner_account_id !== req.accountId) {
      return res.status(403).json({ status: 'error', message: 'Not authorized' });
    }

    const result = await db.query(
      `INSERT INTO world_event_log (world_id, event_type, description, source, event_data)
       VALUES ($1, $2, $3, $4, $5)
       RETURNING id, server_timestamp`,
      [worldId, eventType, description, source || null, JSON.stringify(eventData || {})]
    );

    res.status(201).json({ status: 'success', data: { event: result.rows[0] } });
  })
);

export default router;
