import { randomUUID } from 'node:crypto';
import type { Redis } from 'ioredis';
import { setAIProviderObserver } from './aiProvider.js';

export const AI_PROVIDER_STATUS_KEY = 'ai:gemini:provider-status';

// A late result from an older request must not clear a newer billing failure.
export const WRITE_AI_PROVIDER_STATUS = `
local previous = redis.call('GET', KEYS[1])
local next = cjson.decode(ARGV[1])
if previous then
  local old = cjson.decode(previous)
  if old.requestStartedAt > next.requestStartedAt then return 0 end
  if old.requestStartedAt == next.requestStartedAt and old.state ~= 'healthy' and next.state == 'healthy' then return 0 end
  if old.state == 'balance-exhausted' and next.state == 'quota-exhausted' then return 0 end
  if old.state == next.state then next.incidentId = old.incidentId end
end
redis.call('SET', KEYS[1], cjson.encode(next), 'EX', 2592000)
return 1
`;

export function installAIProviderMonitor(redis: Redis) {
  setAIProviderObserver(async observation => {
    if (!['healthy', 'balance-exhausted', 'quota-exhausted'].includes(observation.state)) return;
    await redis.eval(WRITE_AI_PROVIDER_STATUS, 1, AI_PROVIDER_STATUS_KEY, JSON.stringify({
      ...observation, incidentId: randomUUID(), updatedAt: new Date().toISOString(),
    }));
  });
}
