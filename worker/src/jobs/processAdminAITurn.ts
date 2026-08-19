import { UnrecoverableError, type Job } from 'bullmq';
import { throwIfCancellationRequested } from '../cancellation.js';
import { AdminAIAgentRuntimeError, runAdminAIAgent, type AdminAIAgentResult } from '../services/adminAIAgent.js';
import { AdminAICallbackError, createAdminAICallbackClient, type AdminAICallbackClient } from '../services/adminAICallbackClient.js';
import { logAdminAIEvent, recordAdminAIMetric, safeAdminAITelemetryLabel } from '../services/adminAITelemetry.js';
import { GeminiDeveloperApiError } from '../services/aiProvider.js';

export interface AdminAITurnCompletion extends Record<string, unknown> { schemaVersion: '1'; leaseToken: string; expectedTurnVersion: number; expectedStepNumber: number; expectedBaselineVersion: string; expectedSensitivePolicyVersion: string; decision: AdminAIAgentResult['decision']; decisionHash: string; callbackIdempotencyKey: string; provider: string; model: string; providerResponseId: string | null; inputTokenCount: number | null; outputTokenCount: number | null; latencyMs: number }
export interface AdminAITurnJobData { schemaVersion: '1'; turnId: string; conversationId: string; queuedAt: string; completion?: AdminAITurnCompletion | null }
interface Dependencies { callbacks: AdminAICallbackClient; runAgent: typeof runAdminAIAgent; now: () => number; workerInstanceId: string; cancelled: (job: Job<AdminAITurnJobData>) => Promise<boolean> }

function failureCode(error: unknown) {
  if (error instanceof AdminAIAgentRuntimeError) return failureCode(error.causeError);
  if (error instanceof Error && ['AI_PROVIDER_TIMEOUT', 'CANCELLED', 'TOOL_BUDGET_EXCEEDED'].includes(error.message)) return error.message;
  if (error instanceof Error && (error.name === 'AdminAIDecisionValidationError' || ['AI_INVALID_DECISION', 'ACTION_NOT_ALLOWED', 'READ_CAPABILITY_NOT_ALLOWED', 'REDACTED_CONTEXT_LIMIT'].includes(error.message))) return error.message === 'CANCELLED' ? 'CANCELLED' : error.message === 'TOOL_BUDGET_EXCEEDED' || error.message === 'REDACTED_CONTEXT_LIMIT' ? 'TOOL_BUDGET_EXCEEDED' : 'AI_INVALID_DECISION';
  return 'AI_PROVIDER_FAILURE';
}

function safeFailureDimensions(error: unknown) {
  const cause = error instanceof AdminAIAgentRuntimeError ? error.causeError : error;
  if (cause instanceof GeminiDeveloperApiError) return { failureCategory: safeAdminAITelemetryLabel(cause.category), ...(cause.providerStatus === undefined ? {} : { status: cause.providerStatus }) };
  if (cause instanceof AdminAICallbackError) return { failureCategory: safeAdminAITelemetryLabel(cause.code), ...(cause.httpStatus === undefined ? {} : { status: cause.httpStatus }) };
  return {};
}

export function createAdminAITurnProcessor(overrides: Partial<Dependencies> = {}) {
  const dependencies: Dependencies = { callbacks: overrides.callbacks ?? createAdminAICallbackClient(), runAgent: overrides.runAgent ?? runAdminAIAgent, now: overrides.now ?? Date.now, workerInstanceId: overrides.workerInstanceId ?? `admin-ai-${process.pid}`, cancelled: overrides.cancelled ?? (async job => { try { await throwIfCancellationRequested(job); return false; } catch (error) { if (error instanceof UnrecoverableError) return true; throw error; } }) };
  return async function processAdminAITurn(job: Job<AdminAITurnJobData>) {
    const startedAt = dependencies.now(); const { turnId } = job.data;
    if ((job.name && job.name !== 'respond') || job.data.schemaVersion !== '1' || !turnId) throw new Error('AI_INVALID_JOB');
    const queuedAt = Date.parse(job.data.queuedAt);
    if (Number.isFinite(queuedAt)) recordAdminAIMetric('queue_age', Math.max(0, startedAt - queuedAt), { queue: 'ai-admin-agent-turns' });
    if (job.data.completion) { await dependencies.callbacks.complete(turnId, job.data.completion); recordAdminAIMetric('recovery_outcome', 1, { outcome: 'callback-replayed' }); logAdminAIEvent('callback_replayed', { outcome: 'success', decisionType: job.data.completion.decision.type }); return { success: true, decision: job.data.completion.decision.type, callbackReplay: true }; }
    if (await dependencies.cancelled(job)) return { success: false, reason: 'CANCELLED' };
    const context = await dependencies.callbacks.claim(turnId, dependencies.workerInstanceId); if (!context) return { success: false, reason: 'TURN_NOT_FOUND' };
    const queueAge = dependencies.now() - Date.parse(job.data.queuedAt); const maximumAge = Math.max(30_000, Number.parseInt(process.env.AI_ADMIN_AGENT_MAX_QUEUE_AGE_MS || '300000', 10) || 300_000);
    if (!Number.isFinite(queueAge) || queueAge > maximumAge) { await dependencies.callbacks.fail(turnId, { schemaVersion: '1', leaseToken: context.leaseToken, callbackIdempotencyKey: context.callbackIdempotencyKey, failureCode: 'AI_QUEUE_STALE', provider: null, model: null, latencyMs: 0 }); return { success: false, reason: 'AI_QUEUE_STALE', failureReported: true }; }
    try {
      const result = await dependencies.runAgent(context, dependencies.callbacks, { cancelled: () => dependencies.cancelled(job), now: dependencies.now });
      const completion: AdminAITurnCompletion = { schemaVersion: '1', leaseToken: result.leaseToken, expectedTurnVersion: result.expectedTurnVersion, expectedStepNumber: result.stepNumber, expectedBaselineVersion: context.capabilityBaseline.version, expectedSensitivePolicyVersion: context.sensitiveDataPolicy.version, decision: result.decision, decisionHash: result.decisionHash, callbackIdempotencyKey: context.callbackIdempotencyKey, provider: result.provider, model: result.model, providerResponseId: result.providerResponseId, inputTokenCount: result.inputTokenCount, outputTokenCount: result.outputTokenCount, latencyMs: Math.max(0, dependencies.now() - startedAt) };
      await job.updateData({ ...job.data, completion });
      if (await dependencies.cancelled(job)) return { success: false, reason: 'CANCELLED', completionPending: true };
      await dependencies.callbacks.complete(turnId, completion);
      const providerLabel = safeAdminAITelemetryLabel(completion.provider); const modelLabel = safeAdminAITelemetryLabel(completion.model);
      recordAdminAIMetric('model_latency', completion.latencyMs, { provider: providerLabel, model: modelLabel, decisionType: result.decision.type });
      recordAdminAIMetric('model_outcome', 1, { provider: providerLabel, model: modelLabel, outcome: 'success', decisionType: result.decision.type });
      if (result.decision.type === 'propose_actions') recordAdminAIMetric('proposal_count', result.decision.actions.length, { countBucket: result.decision.actions.length === 1 ? 'one' : 'multiple' });
      logAdminAIEvent('turn_completed', { outcome: 'success', decisionType: result.decision.type });
      return { success: true, decision: result.decision.type, callbackReplay: false };
    } catch (error) {
      if (error instanceof AdminAICallbackError) {
        if (error.retryable) throw error;
        return { success: false, reason: 'CALLBACK_REJECTED' };
      }
      const code = failureCode(error);
      const providerFailure = safeFailureDimensions(error);
      recordAdminAIMetric('model_outcome', 1, { outcome: 'failure', failureCode: code, ...providerFailure });
      logAdminAIEvent('turn_failed', { outcome: 'failure', failureCode: code, ...providerFailure });
      const leaseToken = error instanceof AdminAIAgentRuntimeError ? error.leaseToken : context.leaseToken;
      await dependencies.callbacks.fail(turnId, { schemaVersion: '1', leaseToken, callbackIdempotencyKey: context.callbackIdempotencyKey, failureCode: code, provider: null, model: null, latencyMs: Math.max(0, dependencies.now() - startedAt) });
      return { success: false, reason: code, failureReported: true };
    }
  };
}
export default async function processAdminAITurn(job: Job<AdminAITurnJobData>) { return createAdminAITurnProcessor()(job); }
