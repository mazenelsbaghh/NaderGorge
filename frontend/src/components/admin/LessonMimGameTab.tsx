'use client';

import { useCallback, useEffect, useMemo, useState } from 'react';
import {
  AlertTriangle,
  CheckCircle2,
  Gamepad2,
  LoaderCircle,
  RefreshCw,
  Send,
  ShieldOff,
} from 'lucide-react';
import toast from 'react-hot-toast';

import { LessonMimGameFrame } from '@/components/mim-game/LessonMimGameFrame';
import { getApiErrorSummary } from '@/lib/api-errors';
import {
  canPublishMimGameDraft,
  friendlyMimGameError,
  mimGameProgressKey,
  parseMimGameContent,
  pollMimGameWhileGenerating,
  type LessonMimGameStateDto,
} from '@/lib/mim-game-contract';
import { adminService } from '@/services/admin-service';
import { useAuthStore } from '@/stores/auth-store';

const POLL_INTERVAL_MS = 4_000;
const MAX_POLL_ATTEMPTS = 300;

function timestamp(value?: string | null) {
  if (!value) return null;
  return new Intl.DateTimeFormat('ar-EG', {
    dateStyle: 'medium',
    timeStyle: 'short',
  }).format(new Date(value));
}

function safeRequestError(error: unknown, fallback: string) {
  const status = (error as { response?: { status?: number } })?.response
    ?.status;
  if (status === 401 || status === 403)
    return 'لا تملك صلاحية إدارة لعبة هذه الحصة.';
  return getApiErrorSummary(error, fallback);
}

export function LessonMimGameTab({ lessonId }: { lessonId: string }) {
  const adminId = useAuthStore((state) => state.user?.id);
  const [game, setGame] = useState<LessonMimGameStateDto | null>(null);
  const [loading, setLoading] = useState(true);
  const [action, setAction] = useState<
    'generate' | 'publish' | 'disable' | null
  >(null);
  const [error, setError] = useState('');

  const load = useCallback(
    async (signal?: AbortSignal) => {
      try {
        const state = await adminService.getLessonMimGame(lessonId, signal);
        setGame(state);
        setError('');
        return state;
      } catch (cause) {
        if ((cause as { code?: string })?.code === 'ERR_CANCELED') return null;
        setError(safeRequestError(cause, 'تعذر تحميل حالة لعبة الحصة.'));
        return null;
      } finally {
        setLoading(false);
      }
    },
    [lessonId]
  );

  useEffect(() => {
    const controller = new AbortController();
    void load(controller.signal);
    return () => controller.abort();
  }, [load]);

  useEffect(() => {
    if (game?.status !== 'Generating') return;
    const controller = new AbortController();
    void pollMimGameWhileGenerating({
      load,
      signal: controller.signal,
      intervalMs: POLL_INTERVAL_MS,
      maxAttempts: MAX_POLL_ATTEMPTS,
    }).then((result) => {
      if (result.timedOut)
        setError(
          'استمر التوليد مدة أطول من المتوقع. حدّث الحالة لاحقًا دون إعادة بدء المهمة الآن.'
        );
    });
    return () => controller.abort();
  }, [game?.status, load]);

  const draft = useMemo(
    () =>
      game?.draftContentJson
        ? parseMimGameContent(game.draftContentJson)
        : null,
    [game?.draftContentJson]
  );
  const previewKey =
    draft && adminId && game?.draftFingerprint
      ? mimGameProgressKey({
          userId: adminId,
          lessonId,
          fingerprint: game.draftFingerprint,
          schemaVersion: draft.schemaVersion,
          mode: 'preview',
        })
      : null;

  const runAction = async (kind: 'generate' | 'publish' | 'disable') => {
    setAction(kind);
    setError('');
    try {
      if (kind === 'generate') {
        await adminService.generateLessonMimGame(lessonId);
        toast.success(
          'بدأ توليد مسودة جديدة. النشر والتفعيل لن يحدثا تلقائيًا.'
        );
      } else if (kind === 'publish') {
        await adminService.publishEnableLessonMimGame(lessonId);
        toast.success('تم نشر المسودة الحالية وتفعيل اللعبة للطلاب المؤهلين.');
      } else {
        await adminService.disableLessonMimGame(lessonId);
        toast.success('تم تعطيل لعبة الحصة. بقيت النسخة المنشورة محفوظة.');
      }
      await load();
    } catch (cause) {
      setError(
        safeRequestError(
          cause,
          kind === 'generate'
            ? 'تعذر بدء توليد المسودة.'
            : kind === 'publish'
              ? 'تعذر نشر المسودة.'
              : 'تعذر تعطيل اللعبة.'
        )
      );
    } finally {
      setAction(null);
    }
  };

  if (loading)
    return (
      <div className="admin-panel animate-pulse p-6">
        <div className="h-6 w-44 rounded bg-[var(--admin-card-soft)]" />
        <div className="mt-4 h-24 rounded-xl bg-[var(--admin-card-soft)]" />
      </div>
    );

  return (
    <section className="space-y-5" aria-labelledby="mim-game-heading">
      <div className="admin-panel p-5 sm:p-7">
        <div className="flex flex-col gap-5 lg:flex-row lg:items-start lg:justify-between">
          <div className="max-w-2xl">
            <div className="flex items-center gap-3">
              <span className="admin-badge">
                <Gamepad2 className="h-5 w-5" aria-hidden="true" />
              </span>
              <div>
                <h2
                  id="mim-game-heading"
                  className="text-xl font-black text-[var(--admin-text)]"
                >
                  لعبة ميم للحصة
                </h2>
                <p className="mt-1 text-sm font-medium text-[var(--admin-muted)]">
                  تدريب اختياري من محتوى التحليل. لا يضيف درجات أو ترتيبًا أو
                  مكافآت للطالب.
                </p>
              </div>
            </div>
            <div className="mt-5 flex flex-wrap gap-2 text-xs font-bold">
              <span
                className={`rounded-full px-3 py-1.5 ${game?.isEnabled ? 'bg-emerald-100 text-emerald-800' : 'bg-[var(--admin-card-soft)] text-[var(--admin-muted)]'}`}
              >
                {game?.isEnabled ? 'مفعّلة للطلاب' : 'غير مفعّلة للطلاب'}
              </span>
              <span className="rounded-full bg-[var(--admin-primary-15)] px-3 py-1.5 text-[var(--admin-primary)]">
                {game?.status === 'Generating'
                  ? 'جاري التوليد'
                  : game?.status === 'Ready'
                    ? 'مسودة جاهزة'
                    : game?.status === 'Failed'
                      ? 'فشل التوليد'
                      : game?.status === 'Stale'
                        ? 'المسودة قديمة'
                        : 'لا توجد مسودة'}
              </span>
              {game?.publishedFingerprint && (
                <span className="rounded-full bg-sky-100 px-3 py-1.5 text-sky-800">
                  توجد نسخة منشورة
                </span>
              )}
            </div>
          </div>
          <div className="flex flex-wrap gap-2">
            <button
              type="button"
              disabled={Boolean(action) || game?.status === 'Generating'}
              onClick={() => void runAction('generate')}
              className="admin-btn-ghost min-h-11 px-5 disabled:cursor-not-allowed disabled:opacity-55"
            >
              {action === 'generate' || game?.status === 'Generating' ? (
                <LoaderCircle className="h-4 w-4 animate-spin" />
              ) : (
                <RefreshCw className="h-4 w-4" />
              )}
              {game?.draftContentJson ? 'توليد مسودة جديدة' : 'توليد مسودة'}
            </button>
            {canPublishMimGameDraft(game, draft) && (
              <button
                type="button"
                disabled={Boolean(action)}
                onClick={() => void runAction('publish')}
                className="admin-btn-primary min-h-11 px-5 disabled:opacity-55"
              >
                <Send className="h-4 w-4" />
                نشر وتفعيل هذه المسودة
              </button>
            )}
            {game?.isEnabled && (
              <button
                type="button"
                disabled={Boolean(action)}
                onClick={() => void runAction('disable')}
                className="inline-flex min-h-11 items-center gap-2 rounded-xl border border-red-200 bg-white px-5 text-sm font-black text-red-700 transition-colors hover:bg-red-50 disabled:opacity-55"
              >
                <ShieldOff className="h-4 w-4" />
                تعطيل للطلاب
              </button>
            )}
          </div>
        </div>

        {error && (
          <div
            role="alert"
            className="mt-5 flex items-start gap-3 rounded-xl bg-red-50 p-4 text-sm font-bold text-red-800"
          >
            <AlertTriangle className="mt-0.5 h-5 w-5 shrink-0" />
            <span>{error}</span>
          </div>
        )}
        {game?.lastError && game.status !== 'Generating' && (
          <div className="mt-5 rounded-xl bg-amber-50 p-4 text-sm font-bold text-amber-900">
            {friendlyMimGameError(game.lastError)}
          </div>
        )}
        {!game && !error && (
          <div className="mt-6 rounded-xl bg-[var(--admin-card-soft)] p-5">
            <p className="font-black text-[var(--admin-text)]">
              اللعبة متوقفة افتراضيًا
            </p>
            <p className="mt-2 text-sm leading-7 text-[var(--admin-muted)]">
              ابدأ بتوليد مسودة من تحليل فيديوهات الحصة. بعد اكتمالها، راجع
              الأسئلة والإجابات وعاين العالم ثلاثي الأبعاد، ثم انشرها يدويًا.
            </p>
          </div>
        )}
        {game?.status === 'Generating' && (
          <div className="mt-6 flex items-center gap-3 rounded-xl bg-sky-50 p-4 text-sm font-bold text-sky-900">
            <LoaderCircle className="h-5 w-5 animate-spin" />
            <span>
              يولّد الذكاء الاصطناعي ثلاث مهمات الآن. النسخة المنشورة الحالية،
              إن وجدت، لم تتغير.
            </span>
          </div>
        )}
      </div>

      {draft && (
        <div className="admin-panel p-5 sm:p-7">
          <div className="flex flex-col gap-4 sm:flex-row sm:items-start sm:justify-between">
            <div>
              <div className="flex items-center gap-2">
                <CheckCircle2 className="h-5 w-5 text-emerald-600" />
                <h3 className="text-lg font-black text-[var(--admin-text)]">
                  مراجعة المسودة
                </h3>
              </div>
              <h4 className="mt-3 text-xl font-black text-[var(--admin-text)]">
                {draft.title}
              </h4>
              <p className="mt-2 max-w-3xl text-sm leading-7 text-[var(--admin-muted)]">
                {draft.intro}
              </p>
              <p className="mt-2 text-xs font-bold text-[var(--admin-primary)]">
                المصدر: {draft.sourceLabel}
              </p>
            </div>
            {previewKey && (
              <LessonMimGameFrame
                content={draft}
                progressKey={previewKey}
                mode="preview"
              />
            )}
          </div>
          <div className="mt-6 space-y-5">
            {draft.missions.map((mission, missionIndex) => (
              <article
                key={`${mission.title}-${missionIndex}`}
                className="rounded-xl bg-[var(--admin-card-soft)] p-4 sm:p-5"
              >
                <div className="flex flex-wrap items-start justify-between gap-3">
                  <div>
                    <h5 className="font-black text-[var(--admin-text)]">
                      {missionIndex + 1}. {mission.title}
                    </h5>
                    <p className="mt-1 text-sm leading-6 text-[var(--admin-muted)]">
                      {mission.instruction}
                    </p>
                  </div>
                  <span className="rounded-full bg-white px-3 py-1 text-xs font-bold text-[var(--admin-primary)]">
                    {mission.reward}
                  </span>
                </div>
                <p className="mt-3 text-xs font-bold text-[var(--admin-muted)]">
                  {mission.sourceRefs
                    .map(
                      (source) =>
                        `${Math.floor(source.startTime / 60)}:${String(source.startTime % 60).padStart(2, '0')}–${Math.floor(source.endTime / 60)}:${String(source.endTime % 60).padStart(2, '0')}`
                    )
                    .join('، ')}
                </p>
                <ol className="mt-4 space-y-3">
                  {mission.tasks.map((task, taskIndex) => (
                    <li
                      key={`${task.label}-${taskIndex}`}
                      className="rounded-xl bg-white p-4"
                    >
                      <p className="font-bold text-[var(--admin-text)]">
                        {task.label}
                      </p>
                      <p className="mt-1 text-sm text-emerald-800">
                        الإجابة: {mission.choices[task.correctChoiceIndex]}
                      </p>
                      <p className="mt-1 text-xs leading-6 text-[var(--admin-muted)]">
                        {task.explanation}
                      </p>
                    </li>
                  ))}
                </ol>
              </article>
            ))}
          </div>
          <div className="mt-5 flex flex-wrap gap-x-6 gap-y-2 text-xs font-medium text-[var(--admin-muted)]">
            {timestamp(game?.generatedAtUtc) && (
              <span>توليد المسودة: {timestamp(game?.generatedAtUtc)}</span>
            )}
            {timestamp(game?.publishedAtUtc) && (
              <span>آخر نشر: {timestamp(game?.publishedAtUtc)}</span>
            )}
          </div>
        </div>
      )}
    </section>
  );
}
