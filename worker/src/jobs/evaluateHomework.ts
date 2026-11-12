import type { Job } from 'bullmq';
import { throwIfCancellationRequested } from '../cancellation.js';
import { evaluateHomeworkWithAI } from '../services/geminiService.js';
import { gradeWrittenAnswerWithOpenAI, useOpenAIWrittenGrading } from '../services/openAIWrittenGrading.js';
import { fetchWithTimeout } from '../services/workerFetch.js';
import { withWrittenGradingSlot } from './writtenGradingConcurrency.js';
import { gradeRequestOnly } from '../services/writtenAnswerGradingBoundary.js';

const HOMEWORK_QUESTION_CONCURRENCY = 3;

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
  const grades = await gradePendingQuestions(job, questions);
  await throwIfCancellationRequested(job);
  await deliverGrades({ submissionId, fingerprint, grades });
  await job.updateProgress(100);
  return { success: true };
}

async function gradePendingQuestions(job: Job<HomeworkEvaluationData>, questions: HomeworkQuestion[]) {
  const savedGrades = new Map(job.data.grades?.map(grade => [grade.answerId, grade]));
  const pending: HomeworkQuestion[] = [];
  for (const question of questions) {
    const requestGrade = gradeRequestOnly(question.AnswerText);
    if (requestGrade) {
      savedGrades.set(question.AnswerId, { answerId: question.AnswerId, score: requestGrade.score, feedback: requestGrade.feedback });
    } else if (!savedGrades.has(question.AnswerId)) {
      pending.push(question);
    }
  }
  const grades = [...savedGrades.values()];
  for (let offset = 0; offset < pending.length; offset += HOMEWORK_QUESTION_CONCURRENCY) {
    const batch = pending.slice(offset, offset + HOMEWORK_QUESTION_CONCURRENCY);
    const outcomes = await Promise.allSettled(batch.map(question => gradeQuestion(job, question)));
    const completed = outcomes.flatMap(outcome => outcome.status === 'fulfilled' ? [outcome.value] : []);
    if (completed.length) {
      grades.push(...completed);
      // Keep paid results even when another question fails; write one checkpoint per batch.
      await job.updateData({ ...job.data, grades: [...grades] });
      await job.updateProgress(Math.round(grades.length / questions.length * 90));
    }
    const failed = outcomes.find(outcome => outcome.status === 'rejected');
    if (failed) throw failed.reason;
  }
  return grades;
}

async function gradeQuestion(job: Job<HomeworkEvaluationData>, question: HomeworkQuestion): Promise<HomeworkGrade> {
  await throwIfCancellationRequested(job);
  const evaluation = question.AnswerText.trim()
    ? await withWrittenGradingSlot(async () => {
        await throwIfCancellationRequested(job);
        const grade = useOpenAIWrittenGrading() ? gradeWrittenAnswerWithOpenAI : evaluateHomeworkWithAI;
        return grade(question.AnswerText, question.ExpectedAnswer, question.QuestionText);
      })
    : { score: 0, feedback: 'لم يتم تقديم إجابة مكتوبة لهذا السؤال.' };
  return { answerId: question.AnswerId, score: evaluation.score, feedback: evaluation.feedback };
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
