import { Pool, PoolConfig } from 'pg';
import { config } from '../config';

const pool = new Pool({
  connectionString: config.database.url,
  min: config.database.poolMin,
  max: config.database.poolMax,
} as PoolConfig);

pool.on('error', (err) => {
  console.error('Unexpected database pool error:', err);
});

export const db = {
  query: (text: string, params?: unknown[]) => pool.query(text, params),
  getClient: () => pool.connect(),
  pool,
};

export default db;
