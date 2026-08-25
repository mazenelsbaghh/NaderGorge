import { createHash } from 'node:crypto';
import type { Redis } from 'ioredis';

const ALIAS_TTL_SECONDS = 15 * 24 * 60 * 60;
const ALIAS_VALUE_LIMIT = 180;
const SUPPORTED_QUEUE_NAMES = new Set(['ai-video-chapters', 'generate-chapter-mindmaps']);
const STREAM_ID_PATTERN = /^\d+-\d+$/;

const STORE_NEWEST_ALIAS_SCRIPT = `
local function normalized_decimal(value)
  local normalized = string.gsub(value, '^0+', '')
  if normalized == '' then return '0' end
  return normalized
end

local function greater_decimal(left, right)
  left = normalized_decimal(left)
  right = normalized_decimal(right)
  if string.len(left) ~= string.len(right) then
    return string.len(left) > string.len(right)
  end
  return left > right
end

local function is_newer_or_equal(incoming, current)
  local incoming_separator = string.find(incoming, '-', 1, true)
  local current_separator = string.find(current, '-', 1, true)
  if not incoming_separator or not current_separator then return true end

  local incoming_milliseconds = string.sub(incoming, 1, incoming_separator - 1)
  local incoming_sequence = string.sub(incoming, incoming_separator + 1)
  local current_milliseconds = string.sub(current, 1, current_separator - 1)
  local current_sequence = string.sub(current, current_separator + 1)
  if normalized_decimal(incoming_milliseconds) == normalized_decimal(current_milliseconds) then
    return normalized_decimal(incoming_sequence) == normalized_decimal(current_sequence)
      or greater_decimal(incoming_sequence, current_sequence)
  end
  return greater_decimal(incoming_milliseconds, current_milliseconds)
end

local current_json = redis.call('GET', KEYS[1])
if current_json then
  local decoded, current_alias = pcall(cjson.decode, current_json)
  if decoded and type(current_alias) == 'table'
    and type(current_alias.sourceStreamId) == 'string'
    and not is_newer_or_equal(ARGV[2], current_alias.sourceStreamId) then
    return 0
  end
end

redis.call('SET', KEYS[1], ARGV[1], 'EX', ARGV[3])
return 1
`;

export interface QueuedJobAlias {
  logicalJobId: string;
  physicalJobId: string;
  queueName: string;
}

interface StoredQueuedJobAlias extends QueuedJobAlias {
  sourceStreamId: string;
}

function aliasKey(logicalJobId: string) {
  const digest = createHash('sha256').update(logicalJobId).digest('hex');
  return `job-alias:v1:${digest}`;
}

function validAliasPart(candidate: unknown): candidate is string {
  return typeof candidate === 'string'
    && candidate.length > 0
    && candidate.length <= ALIAS_VALUE_LIMIT;
}

export async function storeQueuedJobAlias(
  redis: Redis,
  alias: QueuedJobAlias,
  sourceStreamId: string,
) {
  if (!validAliasPart(alias.logicalJobId)
    || !validAliasPart(alias.physicalJobId)
    || !SUPPORTED_QUEUE_NAMES.has(alias.queueName)
    || !STREAM_ID_PATTERN.test(sourceStreamId)) {
    throw new Error('Cannot store an invalid queued-job alias.');
  }
  const storedAlias: StoredQueuedJobAlias = { ...alias, sourceStreamId };
  await redis.eval(
    STORE_NEWEST_ALIAS_SCRIPT,
    1,
    aliasKey(alias.logicalJobId),
    JSON.stringify(storedAlias),
    sourceStreamId,
    String(ALIAS_TTL_SECONDS),
  );
}

export async function resolveQueuedJobAlias(redis: Redis, logicalJobId: string) {
  if (!validAliasPart(logicalJobId)) return undefined;
  const serialized = await redis.get(aliasKey(logicalJobId));
  if (!serialized) return undefined;
  let parsed: unknown;
  try {
    parsed = JSON.parse(serialized);
  } catch {
    return undefined;
  }
  if (!parsed || typeof parsed !== 'object') return undefined;
  const alias = parsed as Record<string, unknown>;
  if (alias.logicalJobId !== logicalJobId
    || !validAliasPart(alias.physicalJobId)
    || typeof alias.queueName !== 'string'
    || !SUPPORTED_QUEUE_NAMES.has(alias.queueName)) {
    return undefined;
  }
  return {
    logicalJobId,
    physicalJobId: alias.physicalJobId,
    queueName: alias.queueName,
  };
}
