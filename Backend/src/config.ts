import dotenv from 'dotenv';
dotenv.config();

export const config = {
  port: parseInt(process.env.PORT || '3000', 10),
  nodeEnv: process.env.NODE_ENV || 'development',
  isDev: process.env.NODE_ENV !== 'production',

  database: {
    url: process.env.DATABASE_URL || 'postgresql://bubbleworld:bubbleworld@localhost:5432/bubbleworld',
    poolMin: parseInt(process.env.DATABASE_POOL_MIN || '2', 10),
    poolMax: parseInt(process.env.DATABASE_POOL_MAX || '10', 10),
  },

  redis: {
    url: process.env.REDIS_URL || 'redis://localhost:6379',
  },

  jwt: {
    secret: process.env.JWT_SECRET || 'change-me-in-production',
    accessExpiry: process.env.JWT_ACCESS_EXPIRY || '15m',
    refreshExpiry: process.env.JWT_REFRESH_EXPIRY || '7d',
  },

  pairing: {
    codeExpiryMinutes: parseInt(process.env.PAIRING_CODE_EXPIRY_MINUTES || '5', 10),
    rateLimitPerHour: parseInt(process.env.PAIRING_RATE_LIMIT_PER_HOUR || '10', 10),
  },

  app: {
    // Companion-app store listings. GET /app/download (the "Get the app" QR on
    // Unity's Link Companion App screen) redirects to these by scanning device:
    // Android → Google Play, iOS → App Store. Paste the real links here later.
    googlePlayUrl: process.env.GOOGLE_PLAY_URL || '',
    appStoreUrl: process.env.APP_STORE_URL || '',
    // Phone-reachable base URL of this backend (LAN IP or deployed host).
    // Served via GET /api/v1/ai/status so QR payloads point somewhere phones
    // can reach even when the game client itself connects through localhost.
    publicBaseUrl: process.env.PUBLIC_BASE_URL || '',
  },

  rateLimit: {
    windowMs: parseInt(process.env.RATE_LIMIT_WINDOW_MS || '60000', 10),
    max: parseInt(process.env.RATE_LIMIT_MAX || '100', 10),
  },

  ap: {
    passiveIntervalHours: parseInt(process.env.AP_PASSIVE_INTERVAL_HOURS || '24', 10),
    dailyPassiveAmount: parseInt(process.env.AP_DAILY_PASSIVE_AMOUNT || '1', 10),
    startingBalance: parseInt(process.env.AP_STARTING_BALANCE || '100', 10),
    interventionCost: parseInt(process.env.AP_INTERVENTION_COST || '30', 10),
  },

  bcrypt: {
    rounds: parseInt(process.env.BCRYPT_ROUNDS || '12', 10),
  },

  bedrock: {
    region: process.env.BEDROCK_REGION || process.env.AWS_REGION || 'us-east-1',
    accessKeyId: process.env.BEDROCK_ACCESS_KEY_ID || process.env.AWS_ACCESS_KEY_ID || '',
    secretAccessKey: process.env.BEDROCK_SECRET_ACCESS_KEY || process.env.AWS_SECRET_ACCESS_KEY || '',
    modelId: process.env.BEDROCK_MODEL_ID || 'meta.llama3-8b-instruct-v1:0',
    maxGenLen: parseInt(process.env.BEDROCK_MAX_GEN_LEN || '130', 10),
    temperature: parseFloat(process.env.BEDROCK_TEMPERATURE || '0.7'),
    topP: parseFloat(process.env.BEDROCK_TOP_P || '0.9'),
    timeoutMs: parseInt(process.env.BEDROCK_TIMEOUT_MS || '30000', 10),
  },

  brain: {
    maxRetries: parseInt(process.env.BRAIN_MAX_RETRIES || '2', 10),
    tickMaxRetries: parseInt(process.env.BRAIN_TICK_MAX_RETRIES || '1', 10),
    // Cadence the Unity client waits between brain ticks. Served via /api/v1/ai/status
    // so the backend is the single source of truth (tune server-side, no client rebuild).
    tickSeconds: parseFloat(process.env.BRAIN_TICK_SECONDS || '5'),
    // Shared secret the Unity client sends as x-api-key. Empty = auth disabled (dev only).
    apiKey: process.env.BRAIN_API_KEY || '',
  },
};
