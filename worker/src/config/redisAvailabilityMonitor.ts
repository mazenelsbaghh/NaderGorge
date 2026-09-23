import type { Redis } from 'ioredis';
import { connect } from 'node:net';

const SENTINEL_OUTAGE_ALERT_MS = 15_000;
const SENTINEL_UNREACHABLE_MESSAGE = 'All sentinels are unreachable';

function probeSentinels(redis: Redis): Promise<Array<{ index: number; result: string }>> {
  const sentinels = redis.options?.sentinels ?? [];
  return Promise.all(sentinels.map((sentinel, index) => new Promise<{ index: number; result: string }>((resolve) => {
    const socket = connect({ host: sentinel.host ?? 'localhost', port: sentinel.port ?? 26379 });
    let finished = false;
    const finish = (result: string) => {
      if (finished) return;
      finished = true;
      socket.destroy();
      resolve({ index, result });
    };
    socket.setTimeout(1_000, () => finish('timeout'));
    socket.once('connect', () => finish('reachable'));
    socket.once('error', (error: NodeJS.ErrnoException) => finish(error.code ?? 'socket_error'));
  })));
}

export function monitorRedisSentinelAvailability(redis: Redis, alertDelayMs = SENTINEL_OUTAGE_ALERT_MS) {
  let outageStartedAt: number | undefined;
  let alertTimer: NodeJS.Timeout | undefined;

  redis.on('error', (error) => {
    if (!error.message.includes(SENTINEL_UNREACHABLE_MESSAGE)) {
      console.error('[redis] Redis connection error.', error);
      return;
    }
    if (outageStartedAt !== undefined) return;

    outageStartedAt = Date.now();
    alertTimer = setTimeout(() => {
      console.error('[redis-sentinel] Redis Sentinel outage exceeded the alert threshold.', {
        nodeId: process.env.MASSAR_NODE_ID || 'unknown',
        alertDelayMs,
        redisStatus: redis.status,
        sentinelCount: redis.options?.sentinels?.length ?? 0,
      });
      if (redis.options?.sentinels?.length) void probeSentinels(redis).then((probes) => {
        console.error('[redis-sentinel] Sentinel TCP diagnostics.', {
          nodeId: process.env.MASSAR_NODE_ID || 'unknown',
          probes,
        });
      });
    }, alertDelayMs);
    alertTimer.unref();
  });

  redis.on('ready', () => {
    if (outageStartedAt === undefined) return;

    if (alertTimer) clearTimeout(alertTimer);
    const outageDurationMs = Date.now() - outageStartedAt;
    if (outageDurationMs >= alertDelayMs) {
      console.warn('[redis-sentinel] Redis Sentinel connection recovered.', {
        nodeId: process.env.MASSAR_NODE_ID || 'unknown',
        outageDurationMs,
      });
    }
    outageStartedAt = undefined;
    alertTimer = undefined;
  });
}
