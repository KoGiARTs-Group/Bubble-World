import { Router } from 'express';
import * as companionService from '../services/companionService';
import { requireAuth } from '../middleware/auth';
import { requireWorldAccess } from '../middleware/worldAccess';
import { validate, schemas } from '../middleware/validation';
import { chatLimiter } from '../middleware/rateLimiter';
import { asyncHandler } from '../middleware/errorHandler';
import { AuthenticatedRequest, param, queryParam } from '../types';

const router = Router();

router.post(
  '/:worldId/chat',
  requireAuth,
  requireWorldAccess('member'),
  chatLimiter,
  validate(schemas.chatMessage),
  asyncHandler(async (req: AuthenticatedRequest, res) => {
    const worldId = param(req, 'worldId');
    const { text, sessionId } = req.body;
    const result = await companionService.sendChatMessage(
      req.accountId!, worldId, text, sessionId
    );
    res.status(201).json({ status: 'success', data: result });
  })
);

router.get(
  '/:worldId/chat',
  requireAuth,
  requireWorldAccess('viewer'),
  asyncHandler(async (req: AuthenticatedRequest, res) => {
    const worldId = param(req, 'worldId');
    const sessionId = queryParam(req, 'sessionId');
    const limit = parseInt(queryParam(req, 'limit') || '50', 10);
    const offset = parseInt(queryParam(req, 'offset') || '0', 10);
    const result = await companionService.getChatHistory(
      req.accountId!, worldId, sessionId, limit, offset
    );
    res.json({ status: 'success', data: result });
  })
);

router.get(
  '/:worldId/memories',
  requireAuth,
  requireWorldAccess('viewer'),
  asyncHandler(async (req: AuthenticatedRequest, res) => {
    const worldId = param(req, 'worldId');
    const chapters = await companionService.getMemoryChapters(
      req.accountId!, worldId
    );
    res.json({ status: 'success', data: { chapters } });
  })
);

export default router;
