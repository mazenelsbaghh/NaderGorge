import type { Job } from 'bullmq';
import { throwIfCancellationRequested } from '../cancellation.js';
import { evaluateEssayWithAI } from '../services/geminiService.js';
import { fetchWithTimeout } from '../services/workerFetch.js';

interface HomeworkQuestion {
  AnswerId: string;
  QuestionText: string;
  AnswerText: string;
  ExpectedAnswer: string;
}
interface HomeworkGrade { answerId: string; score: number; feedback: string }
export interface HomeworkEvaluationData {
  SubmissionId: string;
  Fingerprint: string;
  Questions: HomeworkQuestion[];
  grades?: HomeworkGrade[];
}

export async function evaluateHomework(job: Job<HomeworkEvaluationData>) {
  const { SubmissionId: submissionId, Fingerprint: fingerprint, Questions: questions } = job.data;
  if (!submissionId || !/^[A-F0-9]{64}$/.test(fingerprint) || !Array.isArray(questions)
    || questions.length === 0 || questions.length > 500
    || new Set(questions.map(q => q.AnswerId)).size !== questions.length) {
    throw new Error('Invalid homework evaluation payload.');
  }
  const grades = [...(job.data.grades ?? [])];
  for (const question of questions) {
    await throwIfCancellationRequested(job);
    if (grades.some(grade => grade.answerId === question.AnswerId)) continue;
    const evaluation = question.AnswerText.trim()
      ? await evaluateEssayWithAI(question.AnswerText, question.ExpectedAnswer, question.QuestionText)
      : { isCorrect: false, feedback: 'لم يتم تقديم إجابة مكتوبة لهذا السؤال.' };
    grades.push({ answerId: question.AnswerId, score: evaluation.isCorrect ? 1 : 0, feedback: evaluation.feedback });
    // Preserve each paid evaluation across provider failures and callback retries.
    await job.updateData({ ...job.data, grades: [...grades] });
    await job.updateProgress(Math.round(grades.length / questions.length * 90));
  }
  await throwIfCancellationRequested(job);
  await deliverGrades({ submissionId, fingerprint, grades });
  await job.updateProgress(100);
  return { success: true };
}

async function deliverGrades(payload: { submissionId: string; fingerprint: string; grades: HomeworkGrade[] }) {
  const base = (process.env.BACKEND_API_URL || 'http://localhost:5245').replace(/\/$/, '');
  const api = base.endsWith('/api/v1') ? base : `${base}/api/v1`;
  const response = await fetchWithTimeout(`${api}/internal/callbacks/homework-graded`, {
    method: 'POST', timeoutMs: 10_000, maxResponseBytes: 16_384, operation: 'homework-callback',
    headers: { 'Content-Type': 'application/json', 'X-Internal-Token': process.env.AI_CALLBACK_SECRET || process.env.API_CALLBACK_SECRET || '' },
    body: JSON.stringify(payload),
  });
  if (!response.ok) throw new Error(`Homework callback failed with status ${response.status}`);
  const receipt = await response.json() as { success?: boolean; data?: { submissionId?: string; status?: string } };
  if (receipt.success !== true || receipt.data?.submissionId !== payload.submissionId
    || !['Graded', 'PendingReview', 'Deleted', 'Superseded'].includes(receipt.data?.status ?? '')) {
    throw new Error('Homework callback did not confirm a persisted grading result.');
  }
}
