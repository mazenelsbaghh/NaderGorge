import type { Job } from 'bullmq';
import { throwIfCancellationRequested } from '../cancellation.js';
import { generateLiveSupportReply } from '../services/geminiService.js';
import { runLiveSupportAgent } from '../services/liveSupportAgent.js';
import {
  createLiveSupportCallbackClient,
  LiveSupportCallbackError,
  type LiveSupportCallbackClient,
  type LiveSupportCompletionPayload,
} from '../services/liveSupportCallbackClient.js';
import { recordLiveSupportMetric } from '../services/liveSupportTelemetry.js';

export interface LiveSupportTurnJobData {
  schemaVersion: '1';
  turnId: string;
  conversationId: string;
  queuedAt: string;
  completion?: LiveSupportCompletionPayload;
}

interface ProcessorDependencies {
  callbacks: LiveSupportCallbackClient;
  infer: typeof generateLiveSupportReply;
  now: () => number;
}

function safeInferenceFailureCode(error: unknown) {
  if (error instanceof Error && error.message === 'AI_PROVIDER_DEADLINE_EXCEEDED') return 'AI_PROVIDER_TIMEOUT';
  if (error instanceof Error && (error.name === 'LiveSupportDecisionValidationError' || error.message === 'AI_DECISION_NOT_JSON')) return 'AI_INVALID_DECISION';
  return 'AI_PROVIDER_FAILURE';
}

export function createLiveSupportTurnProcessor(overrides: Partial<ProcessorDependencies> = {}) {
  const dependencies: ProcessorDependencies = {
    callbacks: overrides.callbacks ?? createLiveSupportCallbackClient(),
    infer: overrides.infer ?? generateLiveSupportReply,
    now: overrides.now ?? Date.now,
  };

  return async function processLiveSupportTurn(job: Job<LiveSupportTurnJobData>) {
    const startedAt = dependencies.now();
    const { turnId } = job.data;
    const parsedQueuedAt = Date.parse(job.data.queuedAt);
    if (Number.isFinite(parsedQueuedAt)) recordLiveSupportMetric('queue_age', Math.max(0, startedAt - parsedQueuedAt), { queue: 'ai-live-support-turns' });
    await throwIfCancellationRequested(job);

    if (job.data.completion) {
      await dependencies.callbacks.complete(turnId, job.data.completion);
      recordLiveSupportMetric('callback_outcome', 1, { outcome: 'replayed', decisionType: (job.data.completion.decision as { type?: string }).type ?? 'unknown' });
      return { success: true, decision: (job.data.completion.decision as { type?: string }).type, callbackReplay: true };
    }

    const context = await dependencies.callbacks.claim(turnId);
    if (!context) return { success: false, reason: 'TURN_NOT_FOUND' };
    await throwIfCancellationRequested(job);

    const queuedAt = Date.parse(job.data.queuedAt);
    const maximumQueueAgeMs = Math.max(30_000, Number.parseInt(process.env.AI_LIVE_SUPPORT_MAX_QUEUE_AGE_MS || '300000', 10) || 300_000);
    if (!Number.isFinite(queuedAt) || dependencies.now() - queuedAt > maximumQueueAgeMs) {
      await dependencies.callbacks.fail(turnId, {
        failureCode: 'AI_QUEUE_STALE',
        callbackIdempotencyKey: context.callbackIdempotencyKey,
        provider: null,
        model: null,
        latencyMs: 0,
      });
      return { success: false, reason: 'AI_QUEUE_STALE' };
    }

    try {
      let metadata: Awaited<ReturnType<typeof generateLiveSupportReply>> | undefined;
      const validated = await runLiveSupportAgent(context, async prompt => {
        metadata = await dependencies.infer(prompt);
        return metadata.decision;
      });
      const completion: LiveSupportCompletionPayload = {
        schemaVersion: '1',
        expectedConversationVersion: context.expectedConversationVersion,
        expectedPolicyVersionId: context.policyVersionId,
        decision: validated.decision,
        decisionHash: validated.decisionHash,
        callbackIdempotencyKey: context.callbackIdempotencyKey,
        provider: metadata!.provider,
        model: metadata!.model,
        providerResponseId: null,
        inputTokenCount: null,
        outputTokenCount: null,
        latencyMs: Math.max(0, dependencies.now() - startedAt),
      };
      await job.updateData({ ...job.data, completion });
      await throwIfCancellationRequested(job);
      await dependencies.callbacks.complete(turnId, completion);
      recordLiveSupportMetric('inference_latency', completion.latencyMs, { provider: completion.provider, model: completion.model, decisionType: validated.decision.type });
      recordLiveSupportMetric('callback_outcome', 1, { outcome: 'delivered', decisionType: validated.decision.type });
      return { success: true, decision: validated.decision.type, callbackReplay: false };
    } catch (error) {
      if (error instanceof LiveSupportCallbackError) throw error;
      const failureCode = safeInferenceFailureCode(error);
      await dependencies.callbacks.fail(turnId, {
        failureCode,
        callbackIdempotencyKey: context.callbackIdempotencyKey,
        provider: null,
        model: null,
        latencyMs: Math.max(0, dependencies.now() - startedAt),
      });
      throw new Error(failureCode);
    }
  };
}

export default async function processLiveSupportTurn(job: Job<LiveSupportTurnJobData>) {
  return createLiveSupportTurnProcessor()(job);
}
