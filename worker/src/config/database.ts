import { Pool, type PoolConfig } from 'pg';
import { logError } from '../logging.js';

const developmentDatabaseUrl =
  'postgresql://postgres:postgres@localhost:5432/nadergorge?schema=public';

const WORKER_DATABASE_POOL_SIZE = 10;
let sharedPool: Pool | undefined;

export function databaseUrl() {
  const configured = process.env.DATABASE_URL || process.env.DB_CONNECTION_STRING;
  if (configured) return configured;
  if (process.env.NODE_ENV === 'production') {
    throw new Error('DATABASE_URL is required in production.');
  }
  return developmentDatabaseUrl;
}

export function databasePoolConfig(): PoolConfig {
  const nodeId = process.env.MASSAR_NODE_ID?.trim();
  return {
    connectionString: databaseUrl(),
    max: WORKER_DATABASE_POOL_SIZE,
    idleTimeoutMillis: 60_000,
    connectionTimeoutMillis: 15_000,
    application_name: nodeId ? `massar-worker-${nodeId}` : 'massar-worker',
  };
}

/** One PostgreSQL pool shared by every job module in this worker process. */
export function databasePool(): Pool {
  if (!sharedPool) {
    sharedPool = new Pool(databasePoolConfig());
    sharedPool.on('error', error => {
      // pg discards the failed idle client; later queries can open a new one.
      logError('postgres', 'Idle database connection failed.', {
        sqlState: (error as Error & { code?: string }).code ?? 'UNKNOWN',
      });
    });
  }
  return sharedPool;
}
