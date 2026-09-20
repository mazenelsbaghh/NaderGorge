export const MIM_GAME_SCHEMA_VERSION = 1 as const;

export const MIM_GAME_ICONS = [
  'book',
  'lightbulb',
  'target',
  'shield',
  'clock',
  'globe',
  'scale',
  'building',
  'flag',
] as const;

export type MimGameIcon = (typeof MIM_GAME_ICONS)[number];

export interface MimGameSourceRef {
  videoId: string;
  chapterId: string;
  startTime: number;
  endTime: number;
}

export interface MimGameTask {
  label: string;
  icon: MimGameIcon;
  correctChoiceIndex: number;
  explanation: string;
}

export interface MimGameMission {
  title: string;
  instruction: string;
  hint: string;
  reward: string;
  icon: MimGameIcon;
  sourceRefs: MimGameSourceRef[];
  choices: string[];
  tasks: MimGameTask[];
}

export interface MimGameContent {
  schemaVersion: typeof MIM_GAME_SCHEMA_VERSION;
  title: string;
  intro: string;
  sourceLabel: string;
  missions: MimGameMission[];
}

export interface StudentMimGameDto {
  contentJson: string;
  fingerprint: string;
  schemaVersion: number;
}

export type LessonMimGameStatus =
  | 'Draft'
  | 'Generating'
  | 'Ready'
  | 'Failed'
  | 'Stale';

export interface LessonMimGameStateDto {
  id: string;
  status: LessonMimGameStatus;
  isEnabled: boolean;
  draftContentJson?: string | null;
  draftFingerprint?: string | null;
  publishedFingerprint?: string | null;
  generationSourceVideoId?: string | null;
  draftSourceVideoId?: string | null;
  publishedSourceVideoId?: string | null;
  lastError?: string | null;
  generatedAtUtc?: string | null;
  publishedAtUtc?: string | null;
}

const iconSet = new Set<string>(MIM_GAME_ICONS);

function isRecord(value: unknown): value is Record<string, unknown> {
  return typeof value === 'object' && value !== null && !Array.isArray(value);
}

function isSafeText(value: unknown, max: number): value is string {
  return (
    typeof value === 'string' &&
    value.trim().length > 0 &&
    value.length <= max &&
    !/[<>]/.test(value) &&
    !/^\s*(?:javascript:|https?:\/\/)/i.test(value)
  );
}

function isSourceRef(value: unknown): value is MimGameSourceRef {
  if (!isRecord(value)) return false;
  return (
    isSafeText(value.videoId, 80) &&
    isSafeText(value.chapterId, 80) &&
    Number.isInteger(value.startTime) &&
    Number(value.startTime) >= 0 &&
    Number.isInteger(value.endTime) &&
    Number(value.endTime) >= Number(value.startTime)
  );
}

function isTask(value: unknown, choicesCount: number): value is MimGameTask {
  if (!isRecord(value)) return false;
  return (
    isSafeText(value.label, 240) &&
    typeof value.icon === 'string' &&
    iconSet.has(value.icon) &&
    Number.isInteger(value.correctChoiceIndex) &&
    Number(value.correctChoiceIndex) >= 0 &&
    Number(value.correctChoiceIndex) < choicesCount &&
    isSafeText(value.explanation, 400)
  );
}

function isMission(value: unknown): value is MimGameMission {
  if (!isRecord(value) || !Array.isArray(value.choices)) return false;
  const choices = value.choices;
  return (
    isSafeText(value.title, 100) &&
    isSafeText(value.instruction, 500) &&
    isSafeText(value.hint, 300) &&
    isSafeText(value.reward, 100) &&
    typeof value.icon === 'string' &&
    iconSet.has(value.icon) &&
    Array.isArray(value.sourceRefs) &&
    value.sourceRefs.length > 0 &&
    value.sourceRefs.every(isSourceRef) &&
    choices.length >= 2 &&
    choices.length <= 4 &&
    choices.every((choice) => isSafeText(choice, 160)) &&
    Array.isArray(value.tasks) &&
    value.tasks.length >= 3 &&
    value.tasks.length <= 5 &&
    value.tasks.every((task) => isTask(task, choices.length))
  );
}

export function parseMimGameContent(raw: string): MimGameContent | null {
  try {
    const value: unknown = JSON.parse(raw);
    if (!isRecord(value)) return null;
    if (
      value.schemaVersion !== MIM_GAME_SCHEMA_VERSION ||
      !isSafeText(value.title, 120) ||
      !isSafeText(value.intro, 500) ||
      !isSafeText(value.sourceLabel, 160) ||
      !Array.isArray(value.missions) ||
      value.missions.length !== 3 ||
      !value.missions.every(isMission)
    )
      return null;
    return value as unknown as MimGameContent;
  } catch {
    return null;
  }
}

export function canShowStudentMimGame(lesson: {
  isLocked?: boolean;
  isVideoOnlyAccess?: boolean;
  mimGame?: StudentMimGameDto | null;
}): lesson is {
  isLocked?: false;
  isVideoOnlyAccess?: false;
  mimGame: StudentMimGameDto;
} {
  return (
    !lesson.isLocked &&
    !lesson.isVideoOnlyAccess &&
    Boolean(lesson.mimGame?.fingerprint) &&
    lesson.mimGame?.schemaVersion === MIM_GAME_SCHEMA_VERSION &&
    parseMimGameContent(lesson.mimGame.contentJson) !== null
  );
}

export function canPublishMimGameDraft(
  game: LessonMimGameStateDto | null,
  draft: MimGameContent | null
) {
  return game?.status === 'Ready' && draft !== null;
}

export function mimGameProgressKey(input: {
  userId: string;
  lessonId: string;
  fingerprint: string;
  schemaVersion: number;
  mode: 'student' | 'preview';
}) {
  const segment = (value: string) => encodeURIComponent(value.trim());
  return `massar:mim-game:${input.mode}:v${input.schemaVersion}:${segment(input.userId)}:${segment(input.lessonId)}:${segment(input.fingerprint)}`;
}

export async function mimGameContentDigest(content: MimGameContent) {
  const bytes = new TextEncoder().encode(JSON.stringify(content));
  const digest = await crypto.subtle.digest('SHA-256', bytes);
  return Array.from(new Uint8Array(digest), (byte) =>
    byte.toString(16).padStart(2, '0')
  ).join('');
}

export async function mimGameProgressKeyForContent(
  baseProgressKey: string,
  content: MimGameContent
) {
  return `${baseProgressKey}:${await mimGameContentDigest(content)}`;
}

function waitForMimGamePoll(intervalMs: number, signal: AbortSignal) {
  return new Promise<boolean>((resolve) => {
    if (signal.aborted) return resolve(false);
    const timer = globalThis.setTimeout(() => {
      signal.removeEventListener('abort', cancel);
      resolve(true);
    }, intervalMs);
    const cancel = () => {
      globalThis.clearTimeout(timer);
      resolve(false);
    };
    signal.addEventListener('abort', cancel, { once: true });
  });
}

export async function pollMimGameWhileGenerating(input: {
  load: (signal: AbortSignal) => Promise<LessonMimGameStateDto | null>;
  signal: AbortSignal;
  intervalMs: number;
  maxAttempts: number;
}) {
  let attempts = 0;
  while (!input.signal.aborted && attempts < input.maxAttempts) {
    if (!(await waitForMimGamePoll(input.intervalMs, input.signal))) break;
    const state = await input.load(input.signal);
    attempts += 1;
    if (state?.status !== 'Generating') return { attempts, timedOut: false };
  }
  return {
    attempts,
    timedOut: !input.signal.aborted && attempts >= input.maxAttempts,
  };
}

export function friendlyMimGameError(code?: string | null) {
  switch (code) {
    case 'MIM_ANALYSIS_REQUIRED':
      return 'يجب إكمال تحليل فيديوهات الحصة وفصولها أولاً.';
    case 'MIM_MODEL_TIMEOUT':
      return 'استغرق التوليد وقتًا أطول من المتوقع. أعد المحاولة بعد قليل.';
    case 'MIM_SOURCE_CHANGED':
      return 'تغيّر محتوى الحصة أثناء التوليد. أنشئ مسودة جديدة لمراجعة أحدث المحتوى.';
    case 'MIM_DRAFT_NOT_CURRENT':
      return 'المسودة لم تعد مطابقة لمحتوى الحصة. أنشئ مسودة جديدة قبل النشر.';
    default:
      return 'تعذر تجهيز المسودة. أعد المحاولة، وإذا استمرت المشكلة راجع حالة تحليل الفيديوهات.';
  }
}
