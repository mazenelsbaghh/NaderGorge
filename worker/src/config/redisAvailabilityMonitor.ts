import type { Redis } from 'ioredis';

const SENTINEL_OUTAGE_ALERT_MS = 15_000;
const SENTINEL_UNREACHABLE_MESSAGE = 'All sentinels are unreachable';

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
