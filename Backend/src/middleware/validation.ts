import { z, ZodSchema } from 'zod';
import { Request, Response, NextFunction } from 'express';
import { AppError } from './errorHandler';

export function validate<T extends ZodSchema>(schema: T) {
  return (req: Request, _res: Response, next: NextFunction): void => {
    const result = schema.safeParse(req.body);
    if (!result.success) {
      throw new AppError(400, 'Validation error', true, result.error.format());
    }
    req.body = result.data;
    next();
  };
}

export const schemas = {
  register: z.object({
    email: z.string().email().optional(),
    password: z.string().min(8).max(128).optional(),
    displayName: z.string().min(1).max(100),
  }),

  login: z.object({
    email: z.string().email(),
    password: z.string().min(1),
  }),

  platformAuth: z.object({
    platform: z.enum(['nintendo', 'mobile', 'web']),
    platformUserId: z.string().min(1).max(255),
    platformToken: z.string().optional(),
    displayName: z.string().min(1).max(100).optional(),
  }),

  mobileAuth: z.object({
    email: z.string().email(),
    password: z.string().min(1),
    deviceType: z.string().max(50).optional(),
    deviceId: z.string().max(255).optional(),
    deviceName: z.string().max(255).optional(),
  }),

  refreshToken: z.object({
    refreshToken: z.string().min(1),
  }),

  createWorld: z.object({
    name: z.string().min(1).max(255),
    environmentType: z.enum(['MeadowGrassland', 'Forest', 'BeachCoastal']).default('MeadowGrassland'),
    gridWidth: z.number().int().min(10).max(200).default(60),
    gridHeight: z.number().int().min(10).max(200).default(60),
    gridDepth: z.number().int().min(1).max(30).default(8),
  }),

  submitActions: z.object({
    actions: z.array(z.object({
      actionType: z.string().min(1).max(100),
      actionData: z.record(z.string(), z.unknown()),
      clientTimestamp: z.string().optional(),
      batchId: z.string().optional(),
      sequenceIndex: z.number().int().min(0).optional(),
    })).min(1).max(500),
  }),

  createPairing: z.object({
    worldId: z.string().uuid(),
  }),

  claimPairing: z.object({
    shortCode: z.string().min(1).max(10),
  }),

  // Device-side pairing (Unity client, shared x-api-key instead of JWT).
  createDevicePairing: z.object({
    worldKey: z.string().regex(/^[A-Za-z0-9_-]{1,64}$/).optional(),
  }),

  chatMessage: z.object({
    text: z.string().min(1).max(2000),
    sessionId: z.string().uuid().optional(),
  }),

  syncQuery: z.object({
    since: z.string().uuid().optional(),
  }),

  updateProfile: z.object({
    displayName: z.string().min(1).max(100).optional(),
    region: z.string().max(10).optional(),
    preferences: z.record(z.string(), z.unknown()).optional(),
    consentFlags: z.record(z.string(), z.boolean()).optional(),
  }),

  registerDevice: z.object({
    deviceType: z.string().min(1).max(50),
    deviceId: z.string().min(1).max(255),
    deviceName: z.string().max(255).optional(),
  }),

  tick: z.object({
    tickType: z.enum(['scheduled', 'requested', 'offline_catchup']).default('requested'),
    offlineDurationHours: z.number().min(0).max(168).optional(),
  }),

  uploadWorldState: z.object({
    saveVersion: z.string().default('2.1'),
    worldName: z.string().min(1).max(255),
    environmentType: z.string().default('MeadowGrassland'),
    gridWidth: z.number().int().default(60),
    gridHeight: z.number().int().default(60),
    gridDepth: z.number().int().default(8),
    cells: z.record(z.string(), z.unknown()).default({}),
    livabilityPercent: z.number().int().min(0).max(100).default(0),
    weatherCondition: z.string().default('Clear'),
    soulform: z.record(z.string(), z.unknown()).default({}),
    dayCount: z.number().int().min(1).default(1),
    currentSeason: z.number().int().min(0).max(3).default(0),
    architectPoints: z.number().int().min(0).default(100),
    lastPassiveAPTime: z.string().optional(),
    skillData: z.string().default('{}'),
    memoryChapters: z.string().default('[]'),
    tileCompositions: z.record(z.string(), z.unknown()).default({}),
    tileStates: z.record(z.string(), z.unknown()).default({}),
    cooldowns: z.array(z.unknown()).default([]),
    entities: z.array(z.unknown()).default([]),
    bubbles: z.array(z.unknown()).default([]),
  }),

  spawnEntity: z.object({
    entityType: z.enum(['NPC', 'Animal', 'Soulform']),
    gridX: z.number().int().optional(),
    gridY: z.number().int().optional(),
    gridZ: z.number().int().optional(),
    facing: z.enum(['North', 'South', 'East', 'West']).optional(),
    health: z.number().min(0).optional(),
    hunger: z.number().min(0).optional(),
    npcState: z.record(z.string(), z.unknown()).optional(),
    animalState: z.record(z.string(), z.unknown()).optional(),
    soulformEntityState: z.record(z.string(), z.unknown()).optional(),
  }),

  updateEntity: z.object({
    gridX: z.number().int().optional(),
    gridY: z.number().int().optional(),
    gridZ: z.number().int().optional(),
    facing: z.enum(['North', 'South', 'East', 'West']).optional(),
    health: z.number().min(0).optional(),
    hunger: z.number().min(0).optional(),
    pendingMovement: z.enum(['None', 'Wander', 'MoveTo', 'Flee']).optional(),
    targetX: z.number().int().nullable().optional(),
    targetY: z.number().int().nullable().optional(),
    targetZ: z.number().int().nullable().optional(),
    targetEntityId: z.string().nullable().optional(),
    isDead: z.boolean().optional(),
    npcState: z.record(z.string(), z.unknown()).optional(),
    animalState: z.record(z.string(), z.unknown()).optional(),
    soulformEntityState: z.record(z.string(), z.unknown()).optional(),
  }),

  soulformBrain: z.object({
    mode: z.enum(['chat', 'tick']).default('chat'),
    playerMessage: z.string().min(1).max(2000).optional(),
  }),
};
