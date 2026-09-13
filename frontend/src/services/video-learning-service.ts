import { createClientId } from '@/lib/client-id';
import api from './api-client';

export interface LearningTools {
  questions: boolean; understanding: boolean; askTeacher: boolean; notes: boolean;
  bookmarks: boolean; timeline: boolean; cards: boolean; glossary: boolean;
  experiments: boolean; mastery: boolean; review: boolean; aiAuthoring: boolean;
  aiTutor: boolean; aiDailyLimit: number; chapterAids: boolean;
}
export interface LearningActivity {
  id: string; kind: 'question' | 'card' | 'term' | 'experiment' | 'concept';
  placement: 'moment' | 'chapter' | 'end'; seconds: number; endSeconds: number;
  title: string; body: string; answer: string; concept: string; options: string[];
  correctOption: number | null; required: boolean; questionBankId: string | null;
  experiment: 'linear' | 'product' | 'ratio'; factor: number; offset: number; minimum: number; maximum: number;
}
export interface LearningEntry {
  id: string; kind: string; seconds: number; text: string; title: string;
  activityId: string | null; correct: boolean | null; commentId: string | null;
}
export interface TimelineDensity { seconds: number; understood: number; confused: number; example: number }
export interface LearningDocument { tools: LearningTools; activities: LearningActivity[] }
export interface LearningSnapshot {
  version: string; sourceRevision: number; stale: boolean; document: LearningDocument;
  entries: LearningEntry[]; density: TimelineDensity[];
}
export interface LearningReport {
  answers: { activityId: string; students: number; correct: number }[];
  questions: { id: string; seconds: number; text: string; commentId: string; studentName: string }[];
  density: TimelineDensity[]; activities: { id: string; title: string; concept: string; seconds: number }[];
}
export interface LearningReview { id: string; lessonVideoId: string; lessonId: string; videoTitle: string; seconds: number; kind: string; title: string }
const unwrap = <T>(response: { data: { data: T } }) => response.data.data;
export const videoLearningService = {
  read: (id: string, author = false, signal?: AbortSignal) => api.get<{ data: LearningSnapshot }>(`/video-learning/${id}${author ? '/author' : ''}`, { signal }).then(unwrap),
  save: (id: string, snapshot: LearningSnapshot) => api.put<{ data: LearningSnapshot }>(`/video-learning/${id}/author`, {
    version: snapshot.version, sourceRevision: snapshot.sourceRevision, document: snapshot.document,
  }).then(unwrap),
  record: (id: string, version: string, entry: Partial<LearningEntry> & { id: string; kind: string; seconds: number }) =>
    api.post<{ data: { entry: LearningEntry; explanation: string } }>(`/video-learning/${id}/entries`, { version, ...entry }).then(unwrap),
  replies: (id: string, entryId: string) => api.get<{ data: { id: string; body: string; author: string }[] }>(`/video-learning/${id}/entries/${entryId}/replies`).then(unwrap),
  remove: (id: string, entryId: string) => api.delete(`/video-learning/${id}/entries/${entryId}`),
  ai: (id: string, version: string, mode: string, seconds: number, text = '') =>
    api.post<{ data: { text: string; activities: LearningActivity[] } }>(`/video-learning/${id}/ai`,
      { id: createClientId(), version, mode, seconds, text }, { timeout: 70000 }).then(unwrap),
  report: (id: string) => api.get<{ data: LearningReport }>(`/video-learning/${id}/report`).then(unwrap),
  review: () => api.get<{ data: LearningReview[] }>('/video-learning/review').then(unwrap),
};
export function learningError(error: unknown): string {
  const message = (error as { response?: { data?: { message?: string } } })?.response?.data?.message;
  return message || 'تعذر حفظ التغيير. تحقق من الاتصال وحاول مرة أخرى.';
}
export const timeLabel = (seconds: number) => `${Math.floor(seconds / 60)}:${String(Math.floor(seconds % 60)).padStart(2, '0')}`;
export const toolLabels: Record<Exclude<keyof LearningTools, 'aiDailyLimit'>, string> = {
  questions: 'أسئلة الفيديو وتحديات الفصول والختام', understanding: 'فهمت / محتاج مثال / مش فاهم',
  askTeacher: 'اسأل عن اللحظة دي', notes: 'ملاحظاتي', bookmarks: 'لحظات مهمة بنجمة',
  timeline: 'كثافة التفاعلات على التايملاين', cards: 'كروت المراجعة', glossary: 'قاموس الحصة',
  experiments: 'تجارب تفاعلية', mastery: 'قائمة أتقنتها', review: 'مراجعة أجزائي الصعبة',
  chapterAids: 'الفصول والخرائط الذهنية داخل البلاير',
  aiAuthoring: 'توليد مسودات بالذكاء الاصطناعي', aiTutor: 'مساعد الطالب الذكي',
};
export function newActivity(seconds = 0): LearningActivity {
  return { id: createClientId(), kind: 'question', placement: 'moment', seconds, endSeconds: seconds,
    title: '', body: '', answer: '', concept: '', options: ['', ''], correctOption: 0, required: false,
    questionBankId: null, experiment: 'linear', factor: 1, offset: 0, minimum: 0, maximum: 10 };
}
