export type AiJobState = 'waiting' | 'active' | 'completed' | 'failed' | 'not_found';

export type AiJobFailureCode = 'AI_VIDEO_ANALYSIS_FAILED' | 'AI_MINDMAP_GENERATION_FAILED';

export interface AiJobFailure {
  code: AiJobFailureCode;
  message: string;
  retryable: boolean;
}

export interface SafeAiJobStatus {
  id?: string;
  state: AiJobState;
  progress: {
    percentage: number;
    stage: string;
  };
  failure?: AiJobFailure;
}

export interface AiJobProgressEvent {
  jobId: string;
  progress: number;
  status: string;
  message?: string;
}

const JOB_STATES = new Set<AiJobState>([
  'waiting',
  'active',
  'completed',
  'failed',
  'not_found',
]);

const ANALYSIS_FAILURE_MESSAGE =
  'تعذر إكمال تحليل الفيديو. تحقّق من رابط الفيديو وصلاحية الوصول، ثم أعد المحاولة.';
const MINDMAP_FAILURE_MESSAGE =
  'تعذر إكمال توليد الخرائط الذهنية. أعد المحاولة بعد قليل.';

function isRecord(candidate: unknown): candidate is Record<string, unknown> {
  return typeof candidate === 'object' && candidate !== null;
}

function isMindmapJob(jobId: string) {
  return /_(?:mindmap|mindmaps)(?:_|$)/i.test(jobId);
}

function readPercentage(progress: unknown): number {
  const rawPercentage = isRecord(progress) ? progress.percentage : progress;
  const percentage = typeof rawPercentage === 'number' ? rawPercentage : Number(rawPercentage);
  return Number.isFinite(percentage) ? Math.min(100, Math.max(0, percentage)) : 0;
}

function readState(rawState: unknown): AiJobState {
  return typeof rawState === 'string' && JOB_STATES.has(rawState as AiJobState)
    ? (rawState as AiJobState)
    : 'waiting';
}

function getPublicAiJobFailure(jobId: string): AiJobFailure {
  if (isMindmapJob(jobId)) {
    return {
      code: 'AI_MINDMAP_GENERATION_FAILED',
      message: MINDMAP_FAILURE_MESSAGE,
      retryable: true,
    };
  }

  return {
    code: 'AI_VIDEO_ANALYSIS_FAILED',
    message: ANALYSIS_FAILURE_MESSAGE,
    retryable: true,
  };
}

function getAnalysisProgressStage(percentage: number, state: AiJobState): string {
  if (state === 'completed' || percentage >= 100) return 'اكتملت معالجة الفيديو بنجاح.';
  if (percentage < 20) return 'جاري تجهيز الفيديو للتحليل...';
  if (percentage < 60) return 'جاري تحويل صوت المحاضرة إلى نص مكتوب...';
  if (percentage < 85) return 'جاري تقسيم المحاضرة وكتابة الملخصات...';
  if (percentage < 95) return 'جاري بناء الفصول وتجهيز الترجمة...';
  return 'جاري حفظ نتائج التحليل...';
}

function getMindmapProgressStage(percentage: number, state: AiJobState): string {
  if (state === 'completed' || percentage >= 100) return 'اكتمل توليد الخرائط الذهنية بنجاح.';
  if (percentage < 20) return 'جاري تحضير الصور والبيانات اللازمة...';
  if (percentage < 95) return 'جاري توليد الخرائط الذهنية للفصول...';
  return 'جاري حفظ الخرائط في لوحة التحكم...';
}

function getPublicProgressStage(jobId: string, percentage: number, state: AiJobState): string {
  if (state === 'failed') return getPublicAiJobFailure(jobId).message;
  if (state === 'not_found') return 'لا توجد مهمة معالجة نشطة لهذا الفيديو.';
  if (state === 'waiting') return 'جاري التحضير ووضع المهمة في قائمة الانتظار...';
  return isMindmapJob(jobId)
    ? getMindmapProgressStage(percentage, state)
    : getAnalysisProgressStage(percentage, state);
}

/**
 * Treats the worker response as untrusted presentation data. Only the state,
 * bounded percentage and job id survive; worker errors, URLs and commands do not.
 */
export function sanitizeAiJobStatus(input: unknown, fallbackJobId = ''): SafeAiJobStatus {
  const statusPayload = isRecord(input) ? input : {};
  const id = typeof statusPayload.id === 'string' && statusPayload.id ? statusPayload.id : fallbackJobId;
  const state = readState(statusPayload.state);
  const percentage = state === 'completed' ? 100 : readPercentage(statusPayload.progress);
  const failure = state === 'failed' ? getPublicAiJobFailure(id) : undefined;

  return {
    ...(id ? { id } : {}),
    state,
    progress: {
      percentage,
      stage: getPublicProgressStage(id, percentage, state),
    },
    ...(failure ? { failure } : {}),
  };
}

function getProgressEventState(payload: AiJobProgressEvent): AiJobState {
  const normalizedStatus = payload.status.trim().toLowerCase();
  if (normalizedStatus === 'failed' || payload.progress < 0) return 'failed';
  if (normalizedStatus === 'completed' || payload.progress >= 100) return 'completed';
  return normalizedStatus === 'waiting' ? 'waiting' : 'active';
}

/** Builds the same safe status from the SignalR callback contract. */
export function aiJobStatusFromProgressEvent(payload: AiJobProgressEvent): SafeAiJobStatus {
  return sanitizeAiJobStatus(
    {
      id: payload.jobId,
      state: getProgressEventState(payload),
      progress: { percentage: payload.progress },
    },
    payload.jobId,
  );
}
