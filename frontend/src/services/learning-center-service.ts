import apiClient from './api-client';

export interface LearningPackage {
  id: string;
  name: string;
  teacherId: string;
  teacherName: string;
  subjectId: string;
  subjectName: string;
  grade: string;
}
export interface LearningLesson {
  id: string;
  title: string;
  packageId: string;
}
export interface LearningOptions {
  packages: LearningPackage[];
  lessons: LearningLesson[];
  concepts: { lessonId: string; concept: string }[];
}
export interface LearningFilter {
  teacherId?: string;
  subjectId?: string;
  packageId?: string;
  grade?: string;
  days: number;
  inactiveDays: number;
  declinePoints: number;
  repeatedAttempts: number;
}
export interface LearningQuestion {
  id: string;
  text: string;
  type: number;
  points: number;
  tags: string;
  teacherId: string;
  subjectId: string;
  lessonId: string | null;
  concept: string;
  difficulty: number;
  correction: string | null;
  options: { id?: string; text: string; isCorrect: boolean }[];
}
export interface LearningQuestionStats {
  questionId: string;
  text: string;
  students: number;
  attempts: number;
  correctPercent: number | null;
  commonWrongAnswer: string | null;
  commonWrongCount: number;
  discrimination: number | null;
}
export interface LearningConcept {
  lessonId: string;
  lesson: string;
  packageId: string;
  package: string;
  concept: string;
  students: number;
  attempts: number;
  correctPercent: number | null;
  questions: LearningQuestionStats[];
  studentsNeedingReview: {
    studentId: string;
    name: string;
    correctPercent: number;
  }[];
  trend: { date: string; students: number; correctPercent: number | null }[];
}
export interface LearningOverview {
  concepts: LearningConcept[];
  unclassifiedQuestions: number;
  excludedAttempts: number;
  minimumStudents: number;
}
export type FollowUpStatus = 'New' | 'InProgress' | 'Completed';
export interface LearningFollowUp {
  studentId: string;
  name: string;
  packageId: string;
  package: string;
  teacher: string;
  lastActivityAt: string | null;
  reasons: string[];
  status: FollowUpStatus;
  note: string;
  followedUpAt: string | null;
  followedUpBy: string | null;
  latestPercent: number | null;
  improvementPoints: number | null;
}
export interface LearningHistory {
  id: string;
  status: FollowUpStatus;
  note: string;
  reason: string;
  at: string;
  actor: string;
}
export interface QuestionInput {
  text: string;
  lessonId: string;
  concept: string;
  difficulty: number;
  points: number;
  tags: string;
  correction: string | null;
  options: { text: string; isCorrect: boolean }[];
}
export interface BlueprintRow {
  lessonId: string;
  concept: string;
  difficulty: number;
  count: number;
}
export interface GeneratedForm {
  examId: string;
  title: string;
  questions: number;
  totalScore: number;
}
export interface PagedQuestions {
  items: LearningQuestion[];
  totalCount: number;
  page: number;
  pageSize: number;
}
interface Envelope<T> {
  success: boolean;
  data: T;
  message?: string;
}
const base = '/learning-center';
async function read<T>(
  path: string,
  params?: object,
  signal?: AbortSignal
): Promise<T> {
  const response = await apiClient.get<Envelope<T>>(`${base}/${path}`, {
    params,
    signal,
  });
  if (!response.data.success)
    throw new Error(response.data.message || 'تعذر تحميل النتائج');
  return response.data.data;
}
async function save<T>(
  path: string,
  payload: object,
  method: 'post' | 'put' = 'post'
): Promise<T> {
  const response = await apiClient[method]<Envelope<T>>(
    `${base}/${path}`,
    payload
  );
  if (!response.data.success)
    throw new Error(response.data.message || 'تعذر حفظ التغييرات');
  return response.data.data;
}
export const learningCenterService = {
  options: (signal?: AbortSignal) =>
    read<LearningOptions>('options', undefined, signal),
  overview: (filter: LearningFilter, signal?: AbortSignal) =>
    read<LearningOverview>('overview', filter, signal),
  followUps: (filter: LearningFilter, signal?: AbortSignal) =>
    read<LearningFollowUp[]>('follow-ups', filter, signal),
  history: (target: { packageId: string; studentId: string }) =>
    read<LearningHistory[]>('follow-ups/history', target),
  saveFollowUp: (payload: {
    packageId: string;
    studentId: string;
    status: FollowUpStatus;
    note: string;
    reason: string;
  }) => save<void>('follow-ups', payload),
  questions: (filter: object, signal?: AbortSignal) =>
    read<PagedQuestions>('questions', filter, signal),
  saveQuestion: (payload: QuestionInput, id?: string) =>
    save<string>(
      id ? `questions/${id}` : 'questions',
      payload,
      id ? 'put' : 'post'
    ),
  importQuestions: (payload: QuestionInput[]) =>
    save<string[]>('questions/import', payload),
  classify: (
    id: string,
    payload: { lessonId: string; concept: string; difficulty: number }
  ) => save<void>(`questions/${id}/classification`, payload, 'put'),
  generate: (payload: {
    requestId: string;
    packageId: string;
    title: string;
    forms: number;
    durationMinutes: number;
    passingPercent: number;
    blueprint: BlueprintRow[];
  }) => save<GeneratedForm[]>('forms', payload),
};
