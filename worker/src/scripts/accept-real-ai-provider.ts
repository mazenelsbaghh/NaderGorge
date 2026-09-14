import dotenv from 'dotenv';
import { generateLiveSupportReply } from '../services/geminiService.js';
import { readAIConfig } from '../services/aiConfig.js';
import { fetchWithTimeout, redactExternalText } from '../services/workerFetch.js';

dotenv.config();

type EvidenceStatus = 'passed' | 'blocked' | 'failed';

interface EvidenceRecord {
  check: string;
  status: EvidenceStatus;
  detail: string;
}

const records: EvidenceRecord[] = [];

function record(check: string, status: EvidenceStatus, detail: string) {
  records.push({ check, status, detail });
}

function safeError(error: unknown) {
  return redactExternalText(error instanceof Error ? error.message : error);
}

async function checkBackendReadiness() {
  const baseUrl = (process.env.BACKEND_API_URL || 'http://localhost:5245').replace(/\/$/, '');
  const token = process.env.AI_CALLBACK_SECRET;
  if (!token) {
    record('backend-callback-readiness', 'blocked', 'AI_CALLBACK_SECRET is missing; no callback request was attempted.');
    return;
  }
  try {
    const response = await fetchWithTimeout(`${baseUrl}/api/v1/internal/callbacks/live-support-ai/readiness`, {
      headers: { 'X-Internal-Token': token },
      timeoutMs: 3_000,
      maxResponseBytes: 8_192,
    });
    record('backend-callback-readiness', response.ok ? 'passed' : 'failed', `HTTP ${response.status}`);
  } catch (error) {
    record('backend-callback-readiness', 'blocked', safeError(error));
  }
}

async function checkWorkerReadiness() {
  const workerUrl = process.env.WORKER_URL;
  if (!workerUrl) {
    record('worker-readiness', 'blocked', 'WORKER_URL is missing; no worker request was attempted.');
    return;
  }
  try {
    const response = await fetchWithTimeout(`${workerUrl.replace(/\/$/, '')}/ready`, {
      timeoutMs: 3_000,
      maxResponseBytes: 8_192,
    });
    record('worker-readiness', response.ok ? 'passed' : 'failed', `HTTP ${response.status}`);
  } catch (error) {
    record('worker-readiness', 'blocked', safeError(error));
  }
}

async function checkProvider() {
  let config;
  try {
    config = readAIConfig();
    record('provider-configuration', 'passed', `${config.primaryProvider}/${config.textModel}`);
  } catch (error) {
    record('provider-configuration', 'blocked', safeError(error));
    return;
  }

  try {
    const startedAt = Date.now();
    const response = await generateLiveSupportReply({
      systemInstruction: 'Return one valid live-support decision. Do not request secrets or perform actions.',
      contents: [{ role: 'user', parts: [{ text: 'This is a provider acceptance probe. Reply with a short safe Arabic response.' }] }],
      deadlineAt: new Date(Date.now() + 30_000).toISOString(),
    });
    record('real-provider-inference', 'passed', `${response.provider}/${response.model}; latencyMs=${Date.now() - startedAt}; decision=${response.decision.type}`);
  } catch (error) {
    record('real-provider-inference', 'failed', safeError(error));
  }
}

await checkBackendReadiness();
await checkWorkerReadiness();
await checkProvider();

const hasFailure = records.some(item => item.status === 'failed');
const hasBlocker = records.some(item => item.status === 'blocked');
console.log(JSON.stringify({
  harness: 'T119-real-ai-provider',
  generatedAt: new Date().toISOString(),
  usesMocks: false,
  mutatesBusinessData: false,
  records,
}, null, 2));

process.exitCode = hasFailure ? 1 : hasBlocker ? 2 : 0;
