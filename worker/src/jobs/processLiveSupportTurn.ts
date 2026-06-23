import { Job } from 'bullmq';
import { throwIfCancellationRequested } from '../cancellation.js';
import { generateLiveSupportReply } from '../services/geminiService.js';

const API_URL = (() => {
  const base = process.env.BACKEND_API_URL || 'http://localhost:5245';
  return base.endsWith('/api/v1') ? base : `${base}/api/v1`;
})();
const API_CALLBACK_SECRET = process.env.AI_CALLBACK_SECRET || process.env.API_CALLBACK_SECRET;

export interface LiveSupportTurnJobData {
  turnId: string;
  conversationId: string;
}

export default async function processLiveSupportTurn(job: Job<LiveSupportTurnJobData>) {
  const { turnId, conversationId } = job.data;
  console.log(`[LiveSupportTurn] Processing turn ${turnId} for conversation ${conversationId}`);

  let expectedConversationVersion = 0;
  const startTime = Date.now();

  try {
    await throwIfCancellationRequested(job);

    // 1. Claim the turn
    const claimRes = await fetch(`${API_URL}/internal/callbacks/live-support-ai/turns/${turnId}/claim`, {
      method: 'POST',
      headers: {
        'Content-Type': 'application/json',
        'X-Internal-Token': API_CALLBACK_SECRET || ''
      }
    });

    if (!claimRes.ok) {
      if (claimRes.status === 404) {
        console.warn(`[LiveSupportTurn] Turn ${turnId} not found or already processed. Skipping.`);
        return { success: false, reason: 'NOT_FOUND' };
      }
      const errText = await claimRes.text();
      throw new Error(`Failed to claim turn ${turnId}: Status ${claimRes.status} - ${errText}`);
    }

    const context = await claimRes.json() as {
      turnId: string;
      conversationId: string;
      expectedConversationVersion: number;
      systemInstructions: string;
      knowledgeDocuments: string[];
      messages: Array<{ senderType: string; content: string; sentAt: string }>;
    };

    expectedConversationVersion = context.expectedConversationVersion;

    await throwIfCancellationRequested(job);

    // 2. Generate response via Gemini
    console.log(`[LiveSupportTurn] Requesting Gemini decision for turn ${turnId}`);
    const result = await generateLiveSupportReply(
      context.systemInstructions,
      context.knowledgeDocuments,
      context.messages
    );

    const latencyMs = Date.now() - startTime;
    console.log(`[LiveSupportTurn] Gemini response generated for turn ${turnId} in ${latencyMs}ms. Decision: ${result.decision.type}`);

    await throwIfCancellationRequested(job);

    // 3. Complete the turn
    const completeRes = await fetch(`${API_URL}/internal/callbacks/live-support-ai/turns/${turnId}/complete`, {
      method: 'POST',
      headers: {
        'Content-Type': 'application/json',
        'X-Internal-Token': API_CALLBACK_SECRET || ''
      },
      body: JSON.stringify({
        expectedConversationVersion,
        decision: result.decision,
        provider: result.provider,
        model: result.model,
        providerResponseId: null,
        inputTokenCount: null,
        outputTokenCount: null,
        latencyMs,
        callbackIdempotencyKey: turnId
      })
    });

    if (!completeRes.ok) {
      const errText = await completeRes.text();
      throw new Error(`Failed to complete turn ${turnId}: Status ${completeRes.status} - ${errText}`);
    }

    console.log(`[LiveSupportTurn] Successfully completed turn ${turnId}`);
    return { success: true, decision: result.decision.type };

  } catch (error: any) {
    const latencyMs = Date.now() - startTime;
    console.error(`[LiveSupportTurn] Error processing turn ${turnId}:`, error.message);

    // Try to report failure to backend
    try {
      const failRes = await fetch(`${API_URL}/internal/callbacks/live-support-ai/turns/${turnId}/fail`, {
        method: 'POST',
        headers: {
          'Content-Type': 'application/json',
          'X-Internal-Token': API_CALLBACK_SECRET || ''
        },
        body: JSON.stringify({
          failureCode: 'AI_EXECUTION_ERROR',
          safeFailureDetail: error.message,
          provider: 'gemini',
          model: 'unknown',
          latencyMs,
          callbackIdempotencyKey: turnId
        })
      });

      if (!failRes.ok) {
        console.error(`[LiveSupportTurn] Failed to report failure for turn ${turnId}: Status ${failRes.status}`);
      }
    } catch (reportErr: any) {
      console.error(`[LiveSupportTurn] Network error reporting failure for turn ${turnId}:`, reportErr.message);
    }

    throw error;
  }
}
