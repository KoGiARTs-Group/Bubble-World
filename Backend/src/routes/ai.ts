import { Router } from 'express';
import { z } from 'zod';
import * as bedrockService from '../services/bedrockService';
import { config } from '../config';
import { validate } from '../middleware/validation';
import { asyncHandler } from '../middleware/errorHandler';
import { chatLimiter } from '../middleware/rateLimiter';
import { requireSharedKey } from '../middleware/sharedKey';

const router = Router();

// Unity's SoulformBrainService ships the same world-context string the game builds for
// its local brain (Name:/Temperament:/Weather:/Food:/Nearby:/Hunger:/Memories: lines).
// The backend owns prompt building, Bedrock credentials, retries and safety checks.
const chatSchema = z.object({
  worldContext: z.string().min(1).max(20000),
  message: z.string().min(1).max(2000).optional(),
});

// targetX/Y/Z are emitted as strings (or null) because Unity deserializes them into
// string fields via JsonUtility, which does not coerce JSON numbers.
function serializeTarget(v: number | null): string | null {
  return v === null ? null : String(v);
}

// Flat game-JSON response on purpose: Unity's ParseResponse reads the brain fields
// directly off the response body (it does not unwrap a {status,data} envelope here).
router.post(
  '/chat',
  requireSharedKey,
  chatLimiter,
  validate(chatSchema),
  asyncHandler(async (req, res) => {
    const { worldContext, message } = req.body;

    // message omitted → treated as a brain tick ("Tick. Decide what to do.").
    const result = await bedrockService.queryBrain(
      worldContext,
      message ?? null,
      message ? config.brain.maxRetries : config.brain.tickMaxRetries
    );

    const r = result.response;
    res.json({
      currentThought: r.currentThought,
      mood: r.mood,
      currentActivity: r.currentActivity,
      targetX: serializeTarget(r.targetX),
      targetY: serializeTarget(r.targetY),
      targetZ: serializeTarget(r.targetZ),
      expression: r.expression,
      animation: r.animation,
      emotionTag: r.emotionTag,
      action: r.action,
      actionTarget: r.actionTarget,
      // Diagnostics for the client/caller: model === 'mock' means Bedrock was not
      // reachable or not configured and a canned response was served instead.
      model: result.model,
      attempts: result.attempts,
      safetyStatus: result.safetyStatus,
    });
  })
);

// Startup/config endpoint — the client fetches this when it first probes an endpoint
// (replacing a bare /health hit), making the backend the single source of truth for
// brain runtime settings: the Unity config asset only keeps connection links, and
// everything below is applied client-side from this response.
router.get('/status', (_req, res) => {
  res.json({
    status: 'success',
    data: {
      bedrockConfigured: bedrockService.isBedrockConfigured(),
      model: config.bedrock.modelId,
      region: config.bedrock.region,
      brain: {
        tickSeconds: config.brain.tickSeconds,
        maxRetries: config.brain.maxRetries,
        tickMaxRetries: config.brain.tickMaxRetries,
        maxGenLen: config.bedrock.maxGenLen,
        apiKeyRequired: config.brain.apiKey.length > 0,
      },
      features: {
        chat: true,
        tick: true,
        worldSync: true,
      },
      // Phone-reachable base for QR payloads (empty → the client falls back to
      // the link it connected with). Set PUBLIC_BASE_URL in .env to a host
      // phones can reach (LAN IP or deployed URL).
      publicBaseUrl: config.app.publicBaseUrl,
    },
  });
});

export default router;
