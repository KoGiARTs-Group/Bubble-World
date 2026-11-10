import { Request } from 'express';

export interface AuthenticatedRequest extends Request {
  accountId?: string;
  deviceId?: string;
  player?: {
    accountId: string;
    displayName: string;
  };
}

export function param(req: Request, name: string): string {
  const value = req.params[name];
  return Array.isArray(value) ? value[0] : (value || '');
}

export function queryParam(req: Request, name: string): string | undefined {
  const value = req.query[name];
  if (typeof value === 'string') return value;
  if (Array.isArray(value) && typeof value[0] === 'string') return value[0];
  return undefined;
}

export interface WorldStateData {
  saveVersion: string;
  worldName: string;
  environmentType: string;
  gridWidth: number;
  gridHeight: number;
  gridDepth: number;
  cells: Record<string, CellData>;
  livabilityPercent: number;
  weatherCondition: string;
  soulform: SoulformSaveData;
  dayCount: number;
  currentSeason: number;
  architectPoints: number;
  lastPassiveAPTime: string;
  skillData: string;
  memoryChapters: string;
  tileCompositions: Record<string, TileCompositionData>;
  tileStates: Record<string, TileStateData>;
  cooldowns: CooldownData[];
  entities: EntityData[];
  bubbles: BubbleData[];
}

export function cellKey(x: number, y: number, z: number): string {
  return `${x},${y},${z}`;
}

export interface CellData {
  x: number;
  y: number;
  z: number;
  itemId: string;
  layer: number;
  isObject: boolean;
  rotation: number;
  autoTileVariant: number;
  fromIntervention: boolean;
  groundItemId: string;
  tileCategory: string;
  tileHeight: string;
}

export interface TileCompositionData {
  baseMaterial: string;
  surfaceCover: string;
  fluidType: string;
  fluidData: FluidLayerData;
  isHalfTile: boolean;
}

export interface FluidLayerData {
  fluidType: string;
  fluidDepth: number;
  flowRate: number;
  sourceStrength: number;
  northConnection: boolean;
  eastConnection: boolean;
  southConnection: boolean;
  westConnection: boolean;
}

export interface TileStateData {
  currentMoisture: number;
  surfaceWaterAmount: number;
  currentTemperature: number;
  frozenWaterAmount: number;
  currentFertility: number;
  erosionDamage: number;
  contaminationLevel: number;
  fireState: string;
  resourceRemaining: number;
  occupantID: string;
  waterAccumulation: number;
  sunAccumulation: number;
  earthAccumulation: number;
  consecutiveExcessDays: number;
  daysSinceLastCheck: number;
  growthPotential: number;
}

export interface SoulformSaveData {
  name: string;
  meaning: string;
  nature: string;
  temperament: string;
  mood: string;
  gridX: number;
  gridY: number;
  gridZ: number;
  comfort: number;
  hunger: number;
  curiosity: number;
  health: number;
  currentActivity: string;
  currentThought: string;
  currentExpression: string;
  currentAnimation: string;
  appearanceId: string;
  bodyTypeId: string;
  heightId: string;
  faceShapeId: string;
  eyesId: string;
  earsId: string;
  noseId: string;
  skinToneR: number;
  skinToneG: number;
  skinToneB: number;
  markingsId: string;
  skinDetailsId: string;
  hairStyleId: string;
  hairColorR: number;
  hairColorG: number;
  hairColorB: number;
  hairMaterialId: string;
  facialHairId: string;
  voicePresetId: string;
  voicePitch: number;
  voiceTone: number;
  voiceTimbre: number;
  outfitId: string;
  topId: string;
  bottomsId: string;
  footwearId: string;
  accessoryId: string;
  startingAffinity: number;
  initialCalling: string;
  preferredBiome: string;
  bondTraits: string[];
}

export interface CooldownData {
  interventionType: string;
  remainingSeconds: number;
}

export interface ActionData {
  actionType: string;
  actionData: Record<string, unknown>;
  clientTimestamp?: string;
  batchId?: string;
  sequenceIndex?: number;
}

export interface ActionResult {
  actionId: string;
  accepted: boolean;
  reason?: string;
  serverResult?: Record<string, unknown>;
}

export interface LiveSummaryData {
  worldName: string;
  environmentType: string;
  dayCount: number;
  season: string;
  weather: string;
  livability: number;
  population: number;
  soulform: {
    name: string;
    mood: string;
    currentActivity: string;
    currentThought: string;
    health: number;
    comfort: number;
    hunger: number;
    curiosity: number;
  };
  recentEvents: Array<{
    eventType: string;
    description: string;
    timestamp: string;
  }>;
  apBalance: number;
}

export interface ChatMessageData {
  id: string;
  speaker: 'Player' | 'Soulform';
  text: string;
  emotion?: string;
  expression?: string;
  animation?: string;
  portraitId?: string;
  apCost?: number;
  createdAt: string;
}

export interface PairingDisplayData {
  sessionId: string;
  shortCode: string;
  deepLink: string;
  expiresAt: string;
}

export interface SyncDiffData {
  fromVersion: number;
  toVersion: number;
  compressedDiff: string;
}

export type APEntryType = 'passive_grant' | 'intervention_spend' | 'chat_spend' | 'population_reward' | 'skill_reward' | 'admin_grant';

export interface MemoryChapterData {
  id: string;
  chapterIndex: number;
  summary: string | null;
  startTime: string;
  endTime: string | null;
  importantPeople: string[];
  importantPlaces: string[];
  skillsLearned: string[];
  majorEvents: string[];
  entries: MemoryEntryData[];
}

export interface MemoryEntryData {
  id: string;
  description: string;
  category: string;
  keywords: string[];
  createdAt: string;
}

export type EntityType = 'NPC' | 'Animal' | 'Soulform';
export type EntityFacing = 'North' | 'South' | 'East' | 'West';
export type EntityMovementType = 'None' | 'Wander' | 'MoveTo' | 'Flee';

export interface EntityData {
  id: string;
  entityType: EntityType;
  gridX: number;
  gridY: number;
  gridZ: number;
  facing: EntityFacing;
  health: number;
  hunger: number;
  pendingMovement: EntityMovementType;
  targetX: number | null;
  targetY: number | null;
  targetZ: number | null;
  targetEntityId: string | null;
  isDead: boolean;
  npcState?: NPCStateData;
  animalState?: AnimalStateData;
  soulformEntityState?: SoulformEntityStateData;
}

export interface NPCStateData {
  baselineState: string;
  currentState: string;
  currentAction: string;
  isDisgruntled: boolean;
  disgruntledDays: number;
  consecutiveResourceShortageDays: number;
  hasAdequateResources: boolean;
  hasShelter: boolean;
  consecutiveFoodDays: number;
  consecutiveWaterDays: number;
  consecutiveShelterDays: number;
}

export interface AnimalStateData {
  species: string;
  diet: string;
  hungerLevel: number;
  maxHunger: number;
  hungerRate: number;
  currentState: string;
  isAttacked: boolean;
  socialCooldown: number;
  currentFoodTarget: string | null;
}

export interface SoulformEntityStateData {
  currentSkillAttempt: string | null;
  currentActivity: string;
}

export interface BubbleData {
  bubbleId: string;
  environmentType: string;
  gridWidth: number;
  gridHeight: number;
  gridDepth: number;
  seasonOffset: number;
  isUnlocked: boolean;
  connections: BubbleConnectionData[];
}

export interface BubbleConnectionData {
  fromBubbleId: string;
  toBubbleId: string;
  direction: string;
  isOpen: boolean;
}

export type WeatherType = 'Clear' | 'Cloudy' | 'Sunny' | 'Rain' | 'Snow' | 'Windy' | 'Random' | 'Storm1' | 'Storm2' | 'Storm3';
export const VALID_WEATHER_TYPES: WeatherType[] = ['Clear', 'Cloudy', 'Sunny', 'Rain', 'Snow', 'Windy', 'Random', 'Storm1', 'Storm2', 'Storm3'];

export type SeasonType = 'Spring' | 'Summer' | 'Autumn' | 'Winter';

export const SEASONAL_WEATHER_TABLE: Record<number, { weather: WeatherType; weight: number }[]> = {
  0: [ // Spring
    { weather: 'Clear', weight: 30 },
    { weather: 'Cloudy', weight: 25 },
    { weather: 'Rain', weight: 25 },
    { weather: 'Sunny', weight: 10 },
    { weather: 'Windy', weight: 10 },
  ],
  1: [ // Summer
    { weather: 'Sunny', weight: 40 },
    { weather: 'Clear', weight: 25 },
    { weather: 'Cloudy', weight: 15 },
    { weather: 'Storm1', weight: 10 },
    { weather: 'Windy', weight: 10 },
  ],
  2: [ // Autumn
    { weather: 'Cloudy', weight: 30 },
    { weather: 'Rain', weight: 25 },
    { weather: 'Clear', weight: 20 },
    { weather: 'Windy', weight: 15 },
    { weather: 'Storm2', weight: 10 },
  ],
  3: [ // Winter
    { weather: 'Snow', weight: 30 },
    { weather: 'Cloudy', weight: 25 },
    { weather: 'Clear', weight: 15 },
    { weather: 'Windy', weight: 15 },
    { weather: 'Storm3', weight: 15 },
  ],
};
