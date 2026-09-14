import { Job } from 'bullmq';
import { throwIfCancellationRequested } from '../cancellation.js';
import { evaluateEssayWithAI, ESSAY_GRADING_MODEL } from '../services/geminiService.js';
import { fetchWithTimeout } from '../services/workerFetch.js';
const API_URL = (() => {
  const base = process.env.BACKEND_API_URL || 'http://localhost:5245';
  return base.endsWith('/api/v1') ? base : `${base}/api/v1`;
})();
const API_CALLBACK_SECRET = process.env.AI_CALLBACK_SECRET || process.env.API_CALLBACK_SECRET;

export interface EvaluateEssayJobData {
  essaySubmissionId: string;
  questionId: string;
  studentId: string;
  questionText?: string;
  answerText: string;
  expectedAnswer?: string;
  evaluation?: { isCorrect: boolean; feedback: string };
}

export async function processEvaluateEssayJob(job: Job<EvaluateEssayJobData>) {
  const startedAt = Date.now();
  const { essaySubmissionId, questionText, answerText, expectedAnswer } = job.data;
  
  await job.updateProgress({ percentage: 10, stage: 'بنحلل إجابتك...' });
  
  console.log(`[EvaluateEssay] Starting evaluation for essay ${essaySubmissionId}`);

  try {
    await throwIfCancellationRequested(job);

    // A callback retry must not pay for (or wait for) the same AI evaluation again.
    const parsed = job.data.evaluation ?? (answerText.trim()
      ? await evaluateEssayWithAI(answerText, expectedAnswer, questionText)
      : { isCorrect: false, feedback: 'لم يتم تقديم إجابة مكتوبة لهذا السؤال.' });
    if (!job.data.evaluation) await job.updateData({ ...job.data, evaluation: parsed });
    await job.updateProgress({ percentage: 60, stage: 'بنجهّز النتيجة...' });
    await throwIfCancellationRequested(job);

    // Map true/false to 1/0 for the webhook score
    const safeScore = parsed.isCorrect ? 1 : 0;
    
    // Webhook callback to C# API
    await job.updateProgress({ percentage: 80, stage: 'بنبعت النتيجة...' });
    await throwIfCancellationRequested(job);
    
    const webhookResponse = await fetchWithTimeout(`${API_URL}/internal/callbacks/essay-graded`, {
      method: 'POST',
      timeoutMs: 10_000,
      maxResponseBytes: 16_384,
      operation: 'essay-callback',
      headers: {
        'Content-Type': 'application/json',
        'X-Internal-Token': API_CALLBACK_SECRET || ''
      },
      body: JSON.stringify({
        essaySubmissionId,
        aiScore: safeScore,
        aiFeedback: parsed.feedback
      })
    });
    
    if (!webhookResponse.ok) {
       throw new Error(`Essay callback failed with status ${webhookResponse.status}`);
    }
    const receipt = await webhookResponse.json() as { success?: boolean; data?: { essaySubmissionId?: string; status?: string } };
    if (receipt.success !== true || receipt.data?.essaySubmissionId !== essaySubmissionId
      || !['TeacherGraded', 'WaitTeacher', 'AIScored', 'Deleted'].includes(receipt.data?.status ?? '')) {
      throw new Error('Essay callback did not confirm a persisted grading result.');
    }

    await job.updateProgress({ percentage: 100, stage: 'خلصنا التقييم! ✅' });
    console.log(`[EvaluateEssay] Completed successfully for ${essaySubmissionId}`, {
      model: ESSAY_GRADING_MODEL, elapsedMs: Date.now() - startedAt,
    });
    
    return { success: true, score: safeScore, feedback: parsed.feedback };

  } catch (error: unknown) {
    console.error('[EvaluateEssay] Failed', { jobId: job.id, errorName: error instanceof Error ? error.name : 'UnknownError' });
    throw error;
  }
}
