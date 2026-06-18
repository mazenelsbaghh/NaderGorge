import { Job } from 'bullmq';
import { throwIfCancellationRequested } from '../cancellation.js';
import { evaluateEssayWithAI } from '../services/geminiService.js';
const API_URL = (() => {
  const base = process.env.BACKEND_API_URL || 'http://localhost:5245';
  return base.endsWith('/api/v1') ? base : `${base}/api/v1`;
})();
const API_CALLBACK_SECRET = process.env.AI_CALLBACK_SECRET || process.env.API_CALLBACK_SECRET;

export interface EvaluateEssayJobData {
  essaySubmissionId: string;
  questionId: string;
  studentId: string;
  answerText: string;
  expectedAnswer?: string;
}

export async function processEvaluateEssayJob(job: Job<EvaluateEssayJobData>) {
  const { essaySubmissionId, answerText, expectedAnswer } = job.data;
  
  await job.updateProgress({ percentage: 10, stage: 'بنحلل إجابتك...' });
  
  console.log(`[EvaluateEssay] Starting evaluation for essay ${essaySubmissionId}`);

  try {
    await throwIfCancellationRequested(job);

    const parsed = await evaluateEssayWithAI(answerText, expectedAnswer);
    await job.updateProgress({ percentage: 60, stage: 'بنجهّز النتيجة...' });
    await throwIfCancellationRequested(job);

    // Map true/false to 1/0 for the webhook score
    const safeScore = parsed.isCorrect ? 1 : 0;
    
    // Webhook callback to C# API
    await job.updateProgress({ percentage: 80, stage: 'بنبعت النتيجة...' });
    await throwIfCancellationRequested(job);
    
    const webhookResponse = await fetch(`${API_URL}/internal/callbacks/essay-graded`, {
      method: 'POST',
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
       const errBody = await webhookResponse.text();
       throw new Error(`Webhook failed with status ${webhookResponse.status}: ${errBody}`);
    }

    await job.updateProgress({ percentage: 100, stage: 'خلصنا التقييم! ✅' });
    console.log(`[EvaluateEssay] Completed successfully for ${essaySubmissionId}`);
    
    return { success: true, score: safeScore, feedback: parsed.feedback };

  } catch (error: any) {
    console.error(`[EvaluateEssay] Failed:`, error.message);
    throw error;
  }
}
