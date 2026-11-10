import express from 'express';
import cors from 'cors';
import helmet from 'helmet';
import morgan from 'morgan';
import compression from 'compression';
import { config } from './config';
import { notFound, errorHandler } from './middleware/errorHandler';
import { apiLimiter } from './middleware/rateLimiter';
import { runMigrations } from './db/migrate';
import { db } from './db';

import authRoutes from './routes/auth';
import playerRoutes from './routes/players';
import worldRoutes from './routes/worlds';
import pairingRoutes from './routes/pairing';
import companionRoutes from './routes/companion';
import gameDataRoutes from './routes/gameData';
import entityRoutes from './routes/entities';
import aiRoutes from './routes/ai';
import syncRoutes from './routes/sync';
import appLinksRoutes from './routes/appLinks';
import { preload as preloadWorldSync } from './services/worldSyncService';

const app = express();

app.use(helmet());
app.use(cors({
  origin: config.isDev ? '*' : undefined,
  credentials: true,
}));
app.use(compression());
app.use(express.json({ limit: '50mb' }));
app.use(morgan(config.isDev ? 'dev' : 'combined'));

app.set('trust proxy', 1);

app.use('/api/v1', apiLimiter);

app.get('/health', async (_req, res) => {
  let dbStatus = 'ok';
  try {
    await db.query('SELECT 1');
  } catch {
    dbStatus = 'error';
  }

  res.json({
    status: 'ok',
    timestamp: new Date().toISOString(),
    environment: config.nodeEnv,
    database: dbStatus,
  });
});

app.use('/api/v1/auth', authRoutes);
app.use('/api/v1/players', playerRoutes);
app.use('/api/v1/worlds', worldRoutes);
app.use('/api/v1/worlds', entityRoutes);
app.use('/api/v1/pairing', pairingRoutes);
app.use('/api/v1/companion', companionRoutes);
app.use('/api/v1/game-data', gameDataRoutes);
app.use('/api/v1/ai', aiRoutes);
app.use('/api/v1/sync', syncRoutes);

// Public (unauthenticated) companion-app links — outside /api/v1 so phones
// scanning the download QR are not subject to the API rate limiter or key.
app.use('/app', appLinksRoutes);

app.use(notFound);
app.use(errorHandler);

async function start() {
  try {
    console.log('Running database migrations...');
    await runMigrations();
    console.log('Migrations complete.');

    // Single source of truth: bring the authoritative world state into memory
    // before we accept connections. Non-fatal — routes fall back to the DB.
    await preloadWorldSync().catch((err) =>
      console.warn('[world-sync] preload failed (will fall back to DB):', err.message)
    );

    app.listen(config.port, () => {
      console.log(`Bubble World Backend running on port ${config.port}`);
      console.log(`Environment: ${config.nodeEnv}`);
      console.log(`Health check: http://localhost:${config.port}/health`);
    });
  } catch (err) {
    console.error('Failed to start server:', err);
    process.exit(1);
  }
}

start();

export default app;
