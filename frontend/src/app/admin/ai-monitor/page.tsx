'use client';

import { useEffect, useRef, useState } from 'react';
import Link from 'next/link';
import NextImage from 'next/image';
import {
  AlertTriangle,
  BookOpen,
  CheckCircle2,
  ChevronDown,
  Clock3,
  Copy,
  ExternalLink,
  Loader2,
  RefreshCw,
  Sparkles,
  Trash2,
  WifiOff,
  XCircle,
  Zap,
  Image as ImageIcon,
} from 'lucide-react';
import toast from 'react-hot-toast';
import { adminService } from '@/services/admin-service';
import { contentService, type VideoChapterDto, type VideoDto } from '@/services/content-service';
import { AdminShellChrome, AdminTeacherPhotoUpload } from '@/components/admin';

// ─── Types ──────────────────────────────────────────────────────────────────

interface JobProgress {
  percentage: number;
  stage: string;
}

interface JobStatus {
  id: string;
  state: 'waiting' | 'active' | 'completed' | 'failed' | 'not_found';
  progress: JobProgress | number;
  failedReason?: string | null;
}

interface MonitoredVideo {
  video: VideoDto & { lessonId: string; lessonTitle: string; sectionTitle: string };
  jobStatus: JobStatus | null;
  fetchError: boolean;
}

// ─── Progress stages matching the worker exactly ────────────────────────────

const STAGE_CONFIG: Record<string, { label: string; color: string; emoji: string }> = {
  'جاري استخراج وتحضير الصوت من الفيديو...': {
    label: 'استخراج الصوت',
    color: 'oklch(0.65 0.15 200)',
    emoji: '🎵',
  },
  'الذكاء الاصطناعي يقوم بتحليل وتلخيص المحتوى (قد يستغرق دقائق)...': {
    label: 'تحليل Gemini AI',
    color: 'oklch(0.65 0.18 280)',
    emoji: '🤖',
  },
  'جاري بناء هيكل الفصول وإنشاء الترجمة...': {
    label: 'بناء الفصول',
    color: 'oklch(0.65 0.15 140)',
    emoji: '📋',
  },
  'جاري حفظ الفصول وتحديث قواعد البيانات...': {
    label: 'حفظ البيانات',
    color: 'oklch(0.65 0.15 80)',
    emoji: '💾',
  },
  'اكتملت المعالجة بنجاح مئة بالمئة.': {
    label: 'تم بنجاح',
    color: 'oklch(0.65 0.18 145)',
    emoji: '✅',
  },
};

function getStageConfig(stage: string) {
  return STAGE_CONFIG[stage] ?? { label: stage, color: 'oklch(0.65 0.1 70)', emoji: '⚙️' };
}

function getProgressVal(progress: JobProgress | number): number {
  if (typeof progress === 'object' && progress !== null) return progress.percentage ?? 0;
  return Number(progress) || 0;
}

function getProgressStage(progress: JobProgress | number): string {
  if (typeof progress === 'object' && progress !== null && progress.stage) return progress.stage;
  return 'جاري التحضير ووضع المهمة في الطابور...';
}

// ─── Format seconds to mm:ss ─────────────────────────────────────────────────

function formatTime(seconds: number): string {
  const m = Math.floor(seconds / 60);
  const s = Math.floor(seconds % 60);
  return `${m}:${s.toString().padStart(2, '0')}`;
}

// ─── State badges ────────────────────────────────────────────────────────────

function StateBadge({ state }: { state: JobStatus['state'] | null }) {
  if (!state)
    return (
      <span className="ai-badge ai-badge--loading">
        <Loader2 className="h-3 w-3 animate-spin" />
        جاري الجلب
      </span>
    );
  if (state === 'waiting')
    return (
      <span className="ai-badge ai-badge--waiting">
        <Clock3 className="h-3 w-3" />
        في الطابور
      </span>
    );
  if (state === 'active')
    return (
      <span className="ai-badge ai-badge--active">
        <Zap className="h-3 w-3 animate-pulse" />
        نشط الآن
      </span>
    );
  if (state === 'completed' || state === 'not_found')
    return (
      <span className="ai-badge ai-badge--done">
        <CheckCircle2 className="h-3 w-3" />
        اكتمل
      </span>
    );
  if (state === 'failed')
    return (
      <span className="ai-badge ai-badge--failed">
        <AlertTriangle className="h-3 w-3" />
        فشل
      </span>
    );
  return null;
}

// ─── Chapters Panel ──────────────────────────────────────────────────────────

function MindmapJobTracker({ jobId, onDone }: { jobId: string; onDone: () => void }) {
  const [status, setStatus] = useState<{ state: string; progress: JobProgress | number; failedReason?: string | null } | null>(null);
  const doneRef = useRef(false);
  // Track whether we've ever seen the job in a real state (waiting/active)
  // to avoid treating an early not_found as "done"
  const seenJobRef = useRef(false);
  const pollCountRef = useRef(0);
  // Grace period: wait at least 6 polls (~15s) before treating not_found as done
  const GRACE_POLLS = 6;

  useEffect(() => {
    if (!jobId) return;
    doneRef.current = false;
    seenJobRef.current = false;
    pollCountRef.current = 0;

    const poll = async () => {
      if (doneRef.current) return;
      try {
        const res = await fetch(`/api/worker/status/${jobId}`);
        if (!res.ok) return;
        const data = await res.json();
        pollCountRef.current++;

        if (data.state === 'waiting' || data.state === 'active') {
          seenJobRef.current = true;
        }
        setStatus(data);

        if (data.state === 'completed') {
          doneRef.current = true;
          clearInterval(interval);
          setTimeout(onDone, 1500);
        } else if (data.state === 'not_found') {
          // Only treat as done if we already saw the job, or we've been polling long enough
          if (seenJobRef.current || pollCountRef.current >= GRACE_POLLS) {
            doneRef.current = true;
            clearInterval(interval);
            setTimeout(onDone, 1500);
          }
          // Otherwise keep polling — job is still making its way into BullMQ
        } else if (data.state === 'failed') {
          doneRef.current = true;
          clearInterval(interval);
        }
      } catch { /* ignore */ }
    };

    const interval = setInterval(poll, 2500);
    poll();
    return () => clearInterval(interval);
  }, [jobId, onDone]);

  if (!status) {
    return (
      <div className="flex items-center gap-2 text-[11px] text-[var(--admin-muted)] mt-2 px-2 py-1.5 rounded-lg" style={{ background: 'rgba(13,148,136,0.06)' }}>
        <Loader2 className="w-3 h-3 animate-spin text-teal-500" />
        <span>جاري الاتصال بالـ worker...</span>
      </div>
    );
  }

  const pct = getProgressVal(status.progress);
  const stage = typeof status.progress === 'object' && status.progress !== null ? status.progress.stage : '';
  // Only completed if we explicitly got completed state (not not_found in grace period)
  const isCompleted = status.state === 'completed';
  const isPending = status.state === 'not_found';
  const isFailed = status.state === 'failed';

  return (
    <div className="mt-2 rounded-lg border overflow-hidden" style={{ borderColor: isFailed ? 'rgba(239,68,68,0.2)' : isCompleted ? 'rgba(22,163,74,0.2)' : 'rgba(13,148,136,0.2)', background: isFailed ? 'rgba(239,68,68,0.04)' : isCompleted ? 'rgba(22,163,74,0.04)' : 'rgba(13,148,136,0.04)' }}>
      {/* Header row */}
      <div className="flex items-center justify-between px-2.5 py-1.5">
        <div className="flex items-center gap-1.5 text-[11px] font-bold" style={{ color: isFailed ? '#ef4444' : isCompleted ? '#16a34a' : '#0d9488' }}>
          {isFailed ? <AlertTriangle className="w-3 h-3" /> : isCompleted ? <CheckCircle2 className="w-3 h-3" /> : <Loader2 className="w-3 h-3 animate-spin" />}
          {isFailed ? 'فشل التوليد' : isCompleted ? 'اكتمل التوليد ✓' : isPending ? 'في قائمة الانتظار...' : 'جاري التوليد...'}
        </div>
        {!isFailed && !isPending && <span className="text-[10px] font-mono font-bold" style={{ color: '#0d9488' }}>{pct}%</span>}
      </div>
      {/* Progress bar */}
      {!isFailed && (
        <div className="h-1 w-full" style={{ background: 'rgba(0,0,0,0.08)' }}>
          <div
            className="h-full transition-all duration-700"
            style={{
              width: `${pct}%`,
              background: isCompleted ? 'linear-gradient(90deg,#16a34a,#22c55e)' : 'linear-gradient(90deg,#0d9488,#14b8a6)',
            }}
          />
        </div>
      )}
      {/* Stage text */}
      {stage && !isCompleted && (
        <div className="px-2.5 py-1 text-[10px]" style={{ color: 'var(--admin-muted)' }}>{stage}</div>
      )}
      {/* Error reason */}
      {isFailed && status.failedReason && (
        <div className="px-2.5 py-1.5 text-[10px] text-red-500">{status.failedReason}</div>
      )}
    </div>
  );
}

function ChapterRow({ ch, index, videoId }: { ch: VideoChapterDto; index: number; videoId: string }) {
  const [activeJobId, setActiveJobId] = useState<string | null>(null);
  const [sending, setSending] = useState(false);

  const jobId = `${videoId}_mindmap_${ch.id}`;

  const handleRegenerate = async () => {
    if (!confirm(`إعادة توليد خريطة الفصل "${ch.title}"؟`)) return;
    setSending(true);
    try {
      await adminService.regenerateChapterMindmap(ch.id);
      setActiveJobId(jobId);
    } catch {
      toast.error('تعذر إرسال طلب إعادة التوليد');
    } finally {
      setSending(false);
    }
  };

  const handleJobDone = () => {
    setActiveJobId(null);
    toast.success(`تم توليد خريطة "${ch.title}" — أعد تحميل الصفحة لرؤية الصورة`);
  };

  return (
    <div className="ai-chapter-row">
      <div className="ai-chapter-row__num">{index + 1}</div>
      <div className="ai-chapter-row__body">
        <div className="ai-chapter-row__title">{ch.title}</div>
        {ch.summaryText && (
          <div className="ai-chapter-row__summary">{ch.summaryText}</div>
        )}
        {/* Mindmap section */}
        <div className="mt-3">
          {ch.mindmapImageUrl ? (
            <div className="rounded-lg border border-black/10 overflow-hidden relative" style={{ background: 'rgba(0,0,0,0.03)' }}>
              {/* Badge + regen button row */}
              <div className="flex items-center justify-between px-2 py-1 bg-teal-500/10 border-b border-teal-500/15">
                <div className="flex items-center gap-1 text-teal-600 text-[10px] font-bold">
                  <ImageIcon className="w-3 h-3" />
                  خريطة ذهنية
                </div>
                <button
                  onClick={handleRegenerate}
                  disabled={sending || !!activeJobId}
                  title="إعادة توليد الخريطة"
                  className="flex items-center gap-1 text-[10px] font-bold px-2 py-0.5 rounded transition-all disabled:opacity-50"
                  style={{ color: '#0d9488', background: 'rgba(13,148,136,0.1)' }}
                >
                  {sending ? <Loader2 className="w-3 h-3 animate-spin" /> : <RefreshCw className="w-3 h-3" />}
                  {sending ? 'جاري الإرسال...' : 'إعادة توليد'}
                </button>
              </div>
              <a href={`${process.env.NEXT_PUBLIC_BACKEND_URL || 'http://localhost:5245'}${ch.mindmapImageUrl}`} target="_blank" rel="noreferrer" title="تكبير الصورة">
                <NextImage
                  src={`${process.env.NEXT_PUBLIC_BACKEND_URL || 'http://localhost:5245'}${ch.mindmapImageUrl}`}
                  alt={`خريطة ${ch.title}`}
                  width={280}
                  height={158}
                  unoptimized
                  className="h-auto w-full max-w-[280px] opacity-90 transition-opacity hover:opacity-100"
                />
              </a>
            </div>
          ) : (
            /* No mindmap yet — show a generate button */
            <button
              onClick={handleRegenerate}
              disabled={sending || !!activeJobId}
              title="توليد خريطة ذهنية لهذا الفصل"
              className="inline-flex items-center gap-1.5 text-[11px] font-bold px-3 py-1.5 rounded-lg border transition-all disabled:opacity-50"
              style={{ color: '#0d9488', borderColor: 'rgba(13,148,136,0.3)', background: 'rgba(13,148,136,0.08)' }}
            >
              {sending ? <Loader2 className="w-3 h-3 animate-spin" /> : <Sparkles className="w-3 h-3" />}
              {sending ? 'جاري الإرسال...' : 'توليد خريطة ذهنية'}
            </button>
          )}
          {/* Live progress tracker */}
          {activeJobId && (
            <MindmapJobTracker jobId={activeJobId} onDone={handleJobDone} />
          )}
        </div>
      </div>
      <div className="ai-chapter-row__time">
        <span>{formatTime(ch.startTime)}</span>
        <span className="ai-chapter-row__time-sep">–</span>
        <span>{formatTime(ch.endTime)}</span>
      </div>
    </div>
  );
}

function ChaptersPanel({ chapters, videoId }: { chapters: VideoChapterDto[]; videoId: string }) {
  if (!chapters || chapters.length === 0) {
    return (
      <div className="ai-chapters__empty">
        <BookOpen className="h-5 w-5 opacity-40" />
        <span>لا توجد فصول مسجلة لهذا الفيديو</span>
      </div>
    );
  }

  return (
    <div className="ai-chapters">
      <div className="ai-chapters__header">
        <BookOpen className="h-4 w-4" />
        <span>{chapters.length} فصل مولّد بالذكاء الاصطناعي</span>
      </div>
      <div className="ai-chapters__list">
        {chapters.map((ch, i) => (
          <ChapterRow key={ch.id} ch={ch} index={i} videoId={videoId} />
        ))}
      </div>
    </div>
  );
}



// ─── Single Job Card ──────────────────────────────────────────────────────────

function JobCard({
  item,
  onCancel,
  onRetry,
  onDismiss,
}: {
  item: MonitoredVideo;
  onCancel: (videoId: string, isMindmap: boolean) => Promise<void>;
  onRetry: (videoId: string, isMindmap: boolean) => Promise<void>;
  onDismiss: (videoId: string) => void;
}) {
  const { video, jobStatus, fetchError } = item;
  const [actioning, setActioning] = useState(false);
  const [showChapters, setShowChapters] = useState(false);
  const [chapters, setChapters] = useState<VideoChapterDto[] | null>(null);
  const [chaptersLoading, setChaptersLoading] = useState(false);

  const progressVal = jobStatus ? getProgressVal(jobStatus.progress) : 0;
  const progressStage = jobStatus ? getProgressStage(jobStatus.progress) : '';
  const stageConf = getStageConfig(progressStage);
  const state = jobStatus?.state ?? null;
  const isActive = state === 'active' || state === 'waiting';
  const isDone = state === 'completed' || state === 'not_found';
  const isFailed = state === 'failed';

  // Chapters from the video object (already fetched) or lazy-loaded
  const videoChapters = video.chapters ?? chapters ?? [];
  async function loadChapters() {
    if (chapters !== null) return; // already loaded
    if (video.chapters && video.chapters.length > 0) return; // already in video
    setChaptersLoading(true);
    try {
      const res = await contentService.getLessonDetail(video.lessonId);
      const detail = res.data?.data;
      const v = detail?.videos?.find((vv: VideoDto) => vv.id === video.id);
      setChapters(v?.chapters ?? []);
    } catch {
      setChapters([]);
    } finally {
      setChaptersLoading(false);
    }
  }

  const handleToggleChapters = () => {
    const next = !showChapters;
    setShowChapters(next);
    if (next) loadChapters();
  };

  const handleCancel = async () => {
    if (!confirm('هل أنت متأكد من إلغاء هذه المهمة؟')) return;
    setActioning(true);
    try {
      await onCancel(video.id, !!video.isProcessingMindmaps);
    } finally {
      setActioning(false);
    }
  };

  const handleRetry = async () => {
    setActioning(true);
    try {
      await onRetry(video.id, !!video.isProcessingMindmaps);
    } finally {
      setActioning(false);
    }
  };

  return (
    <div className={`ai-card ${isDone ? 'ai-card--done' : ''} ${isFailed ? 'ai-card--failed' : ''}`}>
      {/* Header row */}
      <div className="ai-card__header">
        <div className="ai-card__info">
          <div className="ai-card__video-name">
            {video.title} 
            {video.isProcessingMindmaps && <span className="text-xs text-teal-400 bg-teal-500/10 px-1.5 py-0.5 rounded ml-2 border border-teal-500/20">توليد خرائط ذهنية</span>}
          </div>
          <div className="ai-card__lesson-path">
            <span>{video.sectionTitle}</span>
            <span className="ai-card__path-sep">▸</span>
            <span>{video.lessonTitle}</span>
          </div>
        </div>

        <div className="ai-card__actions">
          <StateBadge state={state} />

          <Link
            href={`/admin/content/lessons/${video.lessonId}`}
            className="ai-icon-btn"
            title="انتقل للدرس"
          >
            <ExternalLink className="h-4 w-4" />
          </Link>

          {isActive && (
            <button
              onClick={handleCancel}
              disabled={actioning}
              className="ai-icon-btn ai-icon-btn--danger"
              title="إلغاء المهمة"
            >
              {actioning ? (
                <Loader2 className="h-4 w-4 animate-spin" />
              ) : (
                <XCircle className="h-4 w-4" />
              )}
            </button>
          )}

          {isFailed && (
            <>
              <button
                onClick={() => {
                  if (jobStatus?.failedReason) {
                    navigator.clipboard.writeText(jobStatus.failedReason);
                    toast.success('تم نسخ الخطأ');
                  }
                }}
                className="ai-icon-btn"
                title="نسخ رسالة الخطأ"
              >
                <Copy className="h-4 w-4" />
              </button>
              <button
                onClick={handleRetry}
                disabled={actioning}
                className="ai-icon-btn ai-icon-btn--gold"
                title="إعادة المحاولة"
              >
                <RefreshCw className={`h-4 w-4 ${actioning ? 'animate-spin' : ''}`} />
              </button>
              <button
                onClick={handleCancel}
                disabled={actioning}
                className="ai-icon-btn ai-icon-btn--danger"
                title="إلغاء وحذف المهمة"
              >
                <XCircle className="h-4 w-4" />
              </button>
            </>
          )}

          {/* ── Wipe Data button — only for done cards ── */}
          {isDone && (
            <button
              onClick={handleCancel}
              disabled={actioning}
              className="ai-icon-btn ai-icon-btn--danger"
              title="مسح الترجمة والفصول بالكامل (إعادة ضبط)"
              style={{ marginRight: '2px' }}
            >
              <Trash2 className="h-4 w-4" />
            </button>
          )}

          {/* ── Dismiss button — only for done or failed cards ── */}
          {(isDone || isFailed) && (
            <button
              onClick={() => onDismiss(video.id)}
              className="ai-icon-btn"
              title="إخفاء هذا الكارت من المراقبة"
              style={{ marginRight: '2px' }}
            >
              <XCircle className="h-4 w-4 opacity-50" />
            </button>
          )}
        </div>
      </div>

      {/* Connection error */}
      {fetchError && (
        <div className="ai-card__error-banner">
          <WifiOff className="h-3.5 w-3.5 shrink-0" />
          <span>تعذر الاتصال بخادم العمل (worker)</span>
        </div>
      )}

      {/* Failed reason */}
      {isFailed && jobStatus?.failedReason && (
        <div className="ai-card__fail-reason" title={jobStatus.failedReason}>
          <AlertTriangle className="h-3.5 w-3.5 shrink-0 text-red-500" />
          <span className="truncate text-[11px] text-red-400">{jobStatus.failedReason}</span>
        </div>
      )}

      {/* Progress section */}
      {!isDone && !isFailed && (
        <div className="ai-card__progress">
          {/* Stage label */}
          <div className="ai-card__stage">
            <span className="ai-card__stage-emoji">{stageConf.emoji}</span>
            <span className="ai-card__stage-label">{progressStage || 'في الطابور...'}</span>
            <span className="ai-card__stage-pct">{progressVal}%</span>
          </div>

          {/* Progress bar */}
          <div className="ai-progress-track">
            <div
              className="ai-progress-fill"
              style={{
                width: `${Math.max(2, progressVal)}%`,
                background: stageConf.color,
              }}
            />
          </div>

          {/* Stage steps visualization */}
          <div className="ai-steps">
            {[
              { pct: 10, label: 'صوت', emoji: '🎵' },
              { pct: 40, label: 'Gemini', emoji: '🤖' },
              { pct: 85, label: 'فصول', emoji: '📋' },
              { pct: 95, label: 'حفظ', emoji: '💾' },
              { pct: 100, label: 'تم', emoji: '✅' },
            ].map((step) => (
              <div
                key={step.pct}
                className={`ai-step ${progressVal >= step.pct ? 'ai-step--done' : progressVal >= step.pct - 30 ? 'ai-step--active' : ''}`}
              >
                <div className="ai-step__dot">{progressVal >= step.pct ? '✓' : step.emoji}</div>
                <div className="ai-step__label">{step.label}</div>
              </div>
            ))}
          </div>
        </div>
      )}

      {/* Done state — show chapters toggle */}
      {isDone && (
        <div className="ai-card__done-row">
          <div className="ai-card__done-msg">
            <CheckCircle2 className="h-4 w-4 text-green-500 shrink-0" />
            <span>اكتمل التحليل — الفصول والترجمة جاهزة</span>
          </div>

          <button
            onClick={handleToggleChapters}
            className={`ai-chapters-toggle ${showChapters ? 'ai-chapters-toggle--open' : ''}`}
          >
            <BookOpen className="h-3.5 w-3.5" />
            <span>عرض الفصول</span>
            <ChevronDown className={`h-3.5 w-3.5 transition-transform duration-200 ${showChapters ? 'rotate-180' : ''}`} />
          </button>
        </div>
      )}

      {/* Chapters expand panel */}
      {isDone && showChapters && (
        <div className="ai-chapters-wrapper">
          {chaptersLoading ? (
            <div className="ai-chapters__loading">
              <Loader2 className="h-4 w-4 animate-spin" />
              <span>جاري تحميل الفصول...</span>
            </div>
          ) : (
            <ChaptersPanel chapters={videoChapters} videoId={video.id} />
          )}
        </div>
      )}
    </div>
  );
}

// ─── Main Page ────────────────────────────────────────────────────────────────

export default function AIMonitorPage() {
  const [items, setItems] = useState<MonitoredVideo[]>([]);
  const [loading, setLoading] = useState(true);
  const [workerReachable, setWorkerReachable] = useState<boolean | null>(null);
  const intervalRef = useRef<NodeJS.Timeout | null>(null);
  // Tracks IDs of videos we're already monitoring — so we never drop them
  // even after isProcessingAI flips to false in the DB.
  const trackedIdsRef = useRef<Set<string>>(new Set());

  // ── Load all processing videos from the content tree ──────────────────────
  async function loadProcessingVideos() {
    try {
      const packagesRes = await contentService.getPackages({ force: true });
      const packages = packagesRes.data?.data ?? [];

      const freshVideos: MonitoredVideo['video'][] = [];

      for (const pkg of packages) {
        const termsRes = await contentService.getTerms(pkg.id);
        const terms = termsRes.data?.data ?? [];

        for (const term of terms) {
          const sectionsRes = await contentService.getSections(term.id);
          const sections = sectionsRes.data?.data ?? [];

          for (const section of sections) {
            const lessonsRes = await contentService.getLessons(section.id);
            const lessons = lessonsRes.data?.data ?? [];

            for (const lesson of lessons) {
              const detailRes = await contentService.getLessonDetail(lesson.id);
              const detail = detailRes.data?.data;
              if (!detail?.videos) continue;

              for (const video of detail.videos) {
                if (video.isProcessingAI || video.isProcessingMindmaps) {
                  freshVideos.push({
                    ...video,
                    lessonId: lesson.id,
                    lessonTitle: lesson.title,
                    sectionTitle: section.title,
                  });
                  trackedIdsRef.current.add(video.id);
                }
              }
            }
          }
        }
      }

      setItems((prev) => {
        const existingMap = new Map(prev.map((i) => [i.video.id, i]));
        const freshMap = new Map(freshVideos.map((v) => [v.id, v]));

        // 1. Start with all fresh (actively-processing) videos from DB
        const merged = new Map<string, MonitoredVideo>();
        for (const video of freshVideos) {
          merged.set(video.id, {
            video,
            jobStatus: existingMap.get(video.id)?.jobStatus ?? null,
            fetchError: existingMap.get(video.id)?.fetchError ?? false,
          });
        }

        // 2. Also keep any previously tracked video that we're still monitoring,
        //    even if the DB has flipped isProcessingAI = false (job just finished).
        //    This prevents the card from vanishing the moment the job completes.
        for (const existing of prev) {
          if (!merged.has(existing.video.id) && trackedIdsRef.current.has(existing.video.id)) {
            // Update video data if a fresh copy exists, otherwise keep stale copy
            const freshVideo = freshMap.get(existing.video.id);
            merged.set(existing.video.id, {
              video: freshVideo ?? existing.video,
              jobStatus: existing.jobStatus,
              fetchError: existing.fetchError,
            });
          }
        }

        return Array.from(merged.values());
      });
    } catch {
      // silently handle
    } finally {
      setLoading(false);
    }
  }

  // ── Poll job statuses from worker ─────────────────────────────────────────
  const pollJobStatuses = async () => {
    setItems((currentItems) => {
      // We can't do async work inside setState, so we fire off the fetches
      // and update state in the callback below.
      void (async () => {
        let anySuccess = false;
        const updated = await Promise.all(
          currentItems.map(async (item) => {
            try {
              const idSuffix = item.video.isProcessingMindmaps ? '_mindmaps' : '';
              const res = await fetch(
                `/api/worker/status/${item.video.id}${idSuffix}`
              );
              if (!res.ok) return { ...item, fetchError: true };
              const data: JobStatus = await res.json();
              anySuccess = true;
              return { ...item, jobStatus: data, fetchError: false };
            } catch {
              return { ...item, fetchError: true };
            }
          })
        );
        setWorkerReachable(anySuccess || currentItems.length === 0);
        setItems(updated);
      })();
      return currentItems; // return unchanged, real update happens above
    });
  };

  useEffect(() => {
    loadProcessingVideos();
  }, []);

  // Start polling once we have items; don't restart when item count changes
  // (that was causing the interval to reset and miss status updates)
  useEffect(() => {
    if (intervalRef.current) clearInterval(intervalRef.current);
    if (items.length === 0) return;
    // Immediate first poll
    void (async () => {
      let anySuccess = false;
      const updated = await Promise.all(
        items.map(async (item) => {
          try {
            const idSuffix = item.video.isProcessingMindmaps ? '_mindmaps' : '';
            const res = await fetch(
              `/api/worker/status/${item.video.id}${idSuffix}`
            );
            if (!res.ok) return { ...item, fetchError: true };
            const data: JobStatus = await res.json();
            anySuccess = true;
            return { ...item, jobStatus: data, fetchError: false };
          } catch {
            return { ...item, fetchError: true };
          }
        })
      );
      setWorkerReachable(anySuccess || items.length === 0);
      setItems(updated);
    })();
    // Recurring poll every 3s
    intervalRef.current = setInterval(pollJobStatuses, 3000);
    return () => {
      if (intervalRef.current) clearInterval(intervalRef.current);
    };
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [items.length]);

  // ── Actions ───────────────────────────────────────────────────────────────
  const handleCancel = async (videoId: string, isMindmap: boolean) => {
    try {
      const idSuffix = isMindmap ? '_mindmaps' : '';
      await fetch(`/api/worker/status/${videoId}${idSuffix}`, { method: 'DELETE' });
      await adminService.cancelVideoAiAnalysis(videoId); // Unlocks both states
      toast.success('تم إلغاء المهمة');
      await loadProcessingVideos();
    } catch {
      toast.error('تعذر إلغاء المهمة');
    }
  };

  const handleRetry = async (videoId: string, isMindmap: boolean) => {
    try {
      const idSuffix = isMindmap ? '_mindmaps' : '';
      await fetch(`/api/worker/status/${videoId}${idSuffix}/retry`, { method: 'POST' });
      toast.success('تمت إعادة المحاولة');
    } catch {
      toast.error('تعذر إعادة المحاولة');
    }
  };

  const handleDismiss = (videoId: string) => {
    trackedIdsRef.current.delete(videoId);
    setItems((prev) => prev.filter((i) => i.video.id !== videoId));
  };

  const handleDismissAllDone = () => {
    setItems((prev) => {
      const toRemove = prev.filter(
        (i) => i.jobStatus?.state === 'completed' || i.jobStatus?.state === 'not_found' || i.jobStatus?.state === 'failed'
      );
      toRemove.forEach((i) => trackedIdsRef.current.delete(i.video.id));
      return prev.filter(
        (i) => i.jobStatus?.state !== 'completed' && i.jobStatus?.state !== 'not_found' && i.jobStatus?.state !== 'failed'
      );
    });
    toast.success('تم مسح الكروت المنتهية');
  };

  const activeCount = items.filter(
    (i) => i.jobStatus?.state === 'active' || i.jobStatus?.state === 'waiting'
  ).length;
  const failedCount = items.filter((i) => i.jobStatus?.state === 'failed').length;
  const doneCount = items.filter(
    (i) => i.jobStatus?.state === 'completed' || i.jobStatus?.state === 'not_found'
  ).length;

  return (
    <AdminShellChrome
      activePath="/admin/ai-monitor"
      sectionLabel="مراقبة الذكاء الاصطناعي"
      pageTitle="مركز تحليل الفيديو"
      subtitle="تابع كل مهام توليد الفصول والترجمة التلقائية — في الوقت الفعلي"
      action={
        <button
          onClick={() => {
            setLoading(true);
            loadProcessingVideos();
          }}
          className="ai-refresh-btn"
          title="تحديث قائمة الفيديوهات"
        >
          <RefreshCw className={`h-4 w-4 ${loading ? 'animate-spin' : ''}`} />
          تحديث
        </button>
      }
    >
      <style>{`
        .ai-monitor { direction: rtl; }

        /* ─── Processing Service Status Banner ─── */
        .ai-worker-banner {
          display: flex; align-items: center; gap: 0.5rem;
          padding: 0.625rem 1rem; border-radius: 0.75rem;
          font-size: 0.8rem; font-weight: 600; margin-bottom: 1.5rem;
        }
        .ai-worker-banner--ok {
          background: oklch(0.62 0.15 145 / 10%); color: oklch(0.55 0.15 145);
          border: 1px solid oklch(0.62 0.15 145 / 20%);
        }
        .ai-worker-banner--err {
          background: oklch(0.65 0.2 25 / 10%); color: oklch(0.55 0.2 25);
          border: 1px solid oklch(0.65 0.2 25 / 20%);
        }

        /* ─── Settings Area ─── */
        .ai-settings-area {
          margin-bottom: 2rem;
        }

        /* ─── Summary chips ─── */
        .ai-summary { display: flex; gap: 1rem; margin-bottom: 2rem; flex-wrap: wrap; }
        .ai-summary-chip {
          display: inline-flex; align-items: center; gap: 0.4rem;
          padding: 0.4rem 0.9rem; border-radius: 9999px;
          font-size: 0.8rem; font-weight: 700; border: 1px solid;
        }
        .ai-summary-chip--active {
          background: oklch(0.65 0.18 280 / 10%); color: oklch(0.55 0.18 280);
          border-color: oklch(0.65 0.18 280 / 20%);
        }
        .ai-summary-chip--failed {
          background: oklch(0.65 0.2 25 / 10%); color: oklch(0.55 0.2 25);
          border-color: oklch(0.65 0.2 25 / 20%);
        }
        .ai-summary-chip--done {
          background: oklch(0.62 0.15 145 / 10%); color: oklch(0.50 0.15 145);
          border-color: oklch(0.62 0.15 145 / 20%);
        }

        /* ─── Cards ─── */
        .ai-card {
          background: var(--admin-card-strong); border: 1px solid var(--admin-border);
          border-radius: 1.25rem; padding: 1.25rem 1.5rem;
          margin-bottom: 1rem; transition: box-shadow 0.2s;
        }
        .ai-card:hover { box-shadow: 0 4px 20px var(--admin-shadow); }
        .ai-card--done { opacity: 0.85; }
        .ai-card--failed {
          border-color: oklch(0.65 0.2 25 / 30%);
          background: oklch(0.65 0.2 25 / 4%);
        }
        .ai-card__header {
          display: flex; align-items: flex-start; justify-content: space-between;
          gap: 1rem; margin-bottom: 0.75rem;
        }
        .ai-card__info { min-width: 0; flex: 1; }
        .ai-card__video-name {
          font-size: 1rem; font-weight: 700; color: var(--admin-text);
          margin-bottom: 0.25rem; white-space: nowrap;
          overflow: hidden; text-overflow: ellipsis;
        }
        .ai-card__lesson-path {
          display: flex; align-items: center; gap: 0.35rem;
          font-size: 0.75rem; color: var(--admin-muted); flex-wrap: wrap;
        }
        .ai-card__path-sep { opacity: 0.5; }
        .ai-card__actions { display: flex; align-items: center; gap: 0.5rem; flex-shrink: 0; }
        .ai-card__error-banner {
          display: flex; align-items: center; gap: 0.5rem; font-size: 0.75rem;
          color: oklch(0.6 0.15 50); background: oklch(0.65 0.15 50 / 8%);
          border: 1px solid oklch(0.65 0.15 50 / 20%); border-radius: 0.5rem;
          padding: 0.4rem 0.75rem; margin-bottom: 0.75rem;
        }
        .ai-card__fail-reason {
          display: flex; align-items: center; gap: 0.5rem;
          font-size: 0.75rem; color: oklch(0.55 0.2 25);
          margin-bottom: 0.75rem; overflow: hidden;
        }

        /* ─── Progress ─── */
        .ai-card__progress { margin-top: 0.25rem; }
        .ai-card__stage { display: flex; align-items: center; gap: 0.4rem; margin-bottom: 0.5rem; }
        .ai-card__stage-emoji { font-size: 0.95rem; }
        .ai-card__stage-label {
          font-size: 0.75rem; font-weight: 600; color: var(--admin-text);
          flex: 1; overflow: hidden; text-overflow: ellipsis; white-space: nowrap;
        }
        .ai-card__stage-pct {
          font-size: 0.75rem; font-weight: 800; color: var(--admin-primary);
          font-variant-numeric: tabular-nums;
        }
        .ai-progress-track {
          height: 6px; background: var(--admin-primary-15); border-radius: 9999px;
          overflow: hidden; margin-bottom: 0.75rem;
        }
        .ai-progress-fill {
          height: 100%; border-radius: 9999px;
          transition: width 0.8s cubic-bezier(0.16, 1, 0.3, 1);
        }

        /* ─── Steps ─── */
        .ai-steps { display: flex; justify-content: space-between; gap: 0.25rem; }
        .ai-step {
          display: flex; flex-direction: column; align-items: center;
          gap: 0.2rem; flex: 1; opacity: 0.35; transition: opacity 0.3s;
        }
        .ai-step--active { opacity: 0.65; }
        .ai-step--done { opacity: 1; }
        .ai-step__dot {
          width: 24px; height: 24px; border-radius: 50%; background: var(--admin-card);
          border: 1.5px solid var(--admin-border); display: flex;
          align-items: center; justify-content: center; font-size: 0.65rem;
          transition: background 0.3s, border-color 0.3s;
        }
        .ai-step--active .ai-step__dot { border-color: var(--admin-primary); background: var(--admin-primary-15); }
        .ai-step--done .ai-step__dot {
          background: var(--admin-primary-15); border-color: var(--admin-primary);
          color: var(--admin-primary); font-size: 0.7rem;
        }
        .ai-step__label {
          font-size: 0.6rem; font-weight: 600; color: var(--admin-muted);
          text-align: center; white-space: nowrap;
        }

        /* ─── Done row ─── */
        .ai-card__done-row {
          display: flex; align-items: center; justify-content: space-between;
          gap: 1rem; flex-wrap: wrap;
        }
        .ai-card__done-msg {
          display: flex; align-items: center; gap: 0.5rem;
          font-size: 0.8rem; font-weight: 600; color: oklch(0.55 0.15 145);
        }

        /* ─── Chapters toggle button ─── */
        .ai-chapters-toggle {
          display: inline-flex; align-items: center; gap: 0.4rem;
          padding: 0.35rem 0.8rem; border-radius: 0.6rem;
          border: 1px solid var(--admin-border); background: var(--admin-card);
          color: var(--admin-primary); font-size: 0.75rem; font-weight: 700;
          cursor: pointer; transition: all 0.15s; white-space: nowrap;
        }
        .ai-chapters-toggle:hover {
          background: var(--admin-primary-15); border-color: var(--admin-primary);
        }
        .ai-chapters-toggle--open {
          background: var(--admin-primary-15); border-color: var(--admin-primary);
        }

        /* ─── Chapters wrapper ─── */
        .ai-chapters-wrapper {
          margin-top: 1rem; border-top: 1px solid var(--admin-border); padding-top: 1rem;
        }
        .ai-chapters__loading {
          display: flex; align-items: center; gap: 0.5rem;
          font-size: 0.8rem; color: var(--admin-muted); padding: 0.75rem 0;
        }
        .ai-chapters__empty {
          display: flex; align-items: center; gap: 0.5rem;
          font-size: 0.8rem; color: var(--admin-muted); padding: 0.75rem 0;
        }

        /* ─── Chapters panel ─── */
        .ai-chapters__header {
          display: flex; align-items: center; gap: 0.4rem;
          font-size: 0.75rem; font-weight: 700; color: var(--admin-primary);
          margin-bottom: 0.75rem; padding-bottom: 0.5rem;
          border-bottom: 1px solid var(--admin-border);
        }
        .ai-chapters__list { display: flex; flex-direction: column; gap: 0.5rem; }

        /* ─── Chapter row ─── */
        .ai-chapter-row {
          display: flex; align-items: flex-start; gap: 0.75rem;
          padding: 0.6rem 0.75rem; border-radius: 0.75rem;
          background: var(--admin-card); border: 1px solid var(--admin-border);
          transition: background 0.15s;
        }
        .ai-chapter-row:hover { background: var(--admin-hover); }
        .ai-chapter-row__num {
          min-width: 24px; height: 24px; border-radius: 50%;
          background: var(--admin-primary-15); color: var(--admin-primary);
          display: flex; align-items: center; justify-content: center;
          font-size: 0.65rem; font-weight: 800; flex-shrink: 0; margin-top: 1px;
        }
        .ai-chapter-row__body { flex: 1; min-width: 0; }
        .ai-chapter-row__title {
          font-size: 0.85rem; font-weight: 700; color: var(--admin-text);
          margin-bottom: 0.15rem;
        }
        .ai-chapter-row__summary {
          font-size: 0.72rem; color: var(--admin-muted); line-height: 1.5;
          overflow: hidden; display: -webkit-box; -webkit-line-clamp: 2;
          -webkit-box-orient: vertical;
        }
        .ai-chapter-row__time {
          display: flex; align-items: center; gap: 0.2rem;
          font-size: 0.7rem; font-weight: 700; font-variant-numeric: tabular-nums;
          color: var(--admin-primary); white-space: nowrap; flex-shrink: 0;
          padding: 0.2rem 0.5rem; border-radius: 0.4rem;
          background: var(--admin-primary-15); margin-top: 1px;
        }
        .ai-chapter-row__time-sep { opacity: 0.5; }

        /* ─── Badges ─── */
        .ai-badge {
          display: inline-flex; align-items: center; gap: 0.3rem;
          padding: 0.25rem 0.65rem; border-radius: 9999px;
          font-size: 0.7rem; font-weight: 700; white-space: nowrap;
        }
        .ai-badge--loading { background: var(--admin-card); color: var(--admin-muted); }
        .ai-badge--waiting { background: oklch(0.65 0.05 70 / 12%); color: oklch(0.55 0.08 70); }
        .ai-badge--active { background: oklch(0.65 0.18 280 / 12%); color: oklch(0.52 0.18 280); }
        .ai-badge--done { background: oklch(0.62 0.15 145 / 12%); color: oklch(0.45 0.15 145); }
        .ai-badge--failed { background: oklch(0.65 0.2 25 / 12%); color: oklch(0.50 0.2 25); }

        /* ─── Icon buttons ─── */
        .ai-icon-btn {
          display: inline-flex; align-items: center; justify-content: center;
          width: 32px; height: 32px; border-radius: 0.5rem;
          border: 1px solid var(--admin-border); background: var(--admin-card);
          color: var(--admin-muted); cursor: pointer; transition: all 0.15s;
        }
        .ai-icon-btn:hover:not(:disabled) { background: var(--admin-hover); color: var(--admin-text); }
        .ai-icon-btn--danger:hover:not(:disabled) {
          background: oklch(0.65 0.2 25 / 10%); color: oklch(0.55 0.2 25);
          border-color: oklch(0.65 0.2 25 / 30%);
        }
        .ai-icon-btn--gold:hover:not(:disabled) {
          background: var(--admin-primary-15); color: var(--admin-primary);
          border-color: var(--admin-primary);
        }
        .ai-icon-btn:disabled { opacity: 0.5; cursor: not-allowed; }

        /* ─── Refresh button ─── */
        .ai-refresh-btn {
          display: inline-flex; align-items: center; gap: 0.4rem;
          padding: 0.5rem 1rem; border-radius: 0.75rem;
          background: var(--admin-card-strong); border: 1px solid var(--admin-border);
          color: var(--admin-text); font-size: 0.8rem; font-weight: 600;
          cursor: pointer; transition: all 0.15s;
        }
        .ai-refresh-btn:hover { background: var(--admin-hover); }

        /* ─── Empty state ─── */
        .ai-empty {
          display: flex; flex-direction: column; align-items: center; justify-content: center;
          padding: 5rem 2rem; text-align: center; gap: 1rem; color: var(--admin-muted);
        }
        .ai-empty__icon {
          width: 64px; height: 64px; border-radius: 50%;
          background: var(--admin-primary-15); display: flex;
          align-items: center; justify-content: center; color: var(--admin-primary);
          margin-bottom: 0.5rem;
        }
        .ai-empty h3 { font-size: 1.1rem; font-weight: 700; color: var(--admin-text); margin: 0; }
        .ai-empty p { font-size: 0.85rem; max-width: 320px; margin: 0; color: var(--admin-muted); line-height: 1.6; }
      `}</style>

      <div className="ai-monitor">
        {/* Processing service reachability banner */}
        {workerReachable === false && items.length > 0 && (
          <div className="ai-worker-banner ai-worker-banner--err">
            <WifiOff className="h-4 w-4 shrink-0" />
            خدمة المعالجة غير متاحة. تأكد من تشغيل خدمة التحليل.
          </div>
        )}
        {workerReachable === true && (
          <div className="ai-worker-banner ai-worker-banner--ok">
            <Zap className="h-4 w-4 shrink-0" />
            خدمة المعالجة تعمل. يجري التحديث كل 3 ثوان تلقائيا
          </div>
        )}

        <div className="ai-settings-area">
          <AdminTeacherPhotoUpload />
        </div>

        {/* Summary counts */}
        {!loading && items.length > 0 && (
          <div className="ai-summary">
            {activeCount > 0 && (
              <span className="ai-summary-chip ai-summary-chip--active">
                <Zap className="h-3.5 w-3.5" />
                {activeCount} نشط
              </span>
            )}
            {failedCount > 0 && (
              <span className="ai-summary-chip ai-summary-chip--failed">
                <AlertTriangle className="h-3.5 w-3.5" />
                {failedCount} فشل
              </span>
            )}
            {doneCount > 0 && (
              <span className="ai-summary-chip ai-summary-chip--done">
                <CheckCircle2 className="h-3.5 w-3.5" />
                {doneCount} اكتمل
              </span>
            )}
            <span className="ai-summary-chip" style={{ opacity: 0.6, borderColor: 'var(--admin-border)', color: 'var(--admin-muted)', background: 'transparent' }}>
              {items.length} فيديو إجمالاً
            </span>
            {/* Clear all done/failed */}
            {(doneCount > 0 || failedCount > 0) && (
              <button
                onClick={handleDismissAllDone}
                className="ai-summary-chip"
                style={{ cursor: 'pointer', borderColor: 'oklch(0.65 0.2 25 / 25%)', color: 'oklch(0.55 0.2 25)', background: 'oklch(0.65 0.2 25 / 6%)' }}
                title="إخفاء جميع الكروت المنتهية"
              >
                <XCircle className="h-3.5 w-3.5" />
                مسح المنتهية
              </button>
            )}
          </div>
        )}

        {/* Loading skeleton */}
        {loading && (
          <div className="space-y-3">
            {[1, 2, 3].map((i) => (
              <div key={i} className="ai-card" style={{ opacity: 0.5 }}>
                <div style={{ height: 20, width: '60%', borderRadius: 6, background: 'var(--admin-hover)' }} />
                <div style={{ height: 12, width: '40%', borderRadius: 4, background: 'var(--admin-hover)', marginTop: 8 }} />
                <div style={{ height: 6, width: '100%', borderRadius: 9999, background: 'var(--admin-hover)', marginTop: 16 }} />
              </div>
            ))}
          </div>
        )}

        {/* Jobs list */}
        {!loading && items.length > 0 && (
          <div>
            {items.map((item) => (
              <JobCard
                key={item.video.id}
                item={item}
                onCancel={handleCancel}
                onRetry={handleRetry}
                onDismiss={handleDismiss}
              />
            ))}
          </div>
        )}

        {/* Empty state */}
        {!loading && items.length === 0 && (
          <div className="ai-empty">
            <div className="ai-empty__icon">
              <Sparkles className="h-7 w-7" />
            </div>
            <h3>لا توجد مهام تحليل حالية</h3>
            <p>
              الفيديوهات التي تحمل علامة معالجة AI ستظهر هنا.
              <br />
              اذهب لأي درس واضغط على ✨ لبدء تحليل فيديو جديد.
            </p>
            <Link href="/admin/content" className="ai-refresh-btn" style={{ marginTop: '0.5rem' }}>
              <ExternalLink className="h-4 w-4" />
              انتقل لإدارة المحتوى
            </Link>
          </div>
        )}
      </div>
    </AdminShellChrome>
  );
}
