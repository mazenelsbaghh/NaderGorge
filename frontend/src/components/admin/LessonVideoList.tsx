'use client';
import { useState, useEffect, useRef } from 'react';
import NextImage from 'next/image';
import { PlaySquare, Trash2, Edit2, GripVertical, Sparkles, Loader2, AlertTriangle, XCircle, RefreshCw, BookOpen, BookCheck, ChevronDown, Image as ImageIcon, Play, X, Eye, EyeOff, ZoomIn } from 'lucide-react';
import { ContentArchiveControl } from './ContentArchiveControl';
import toast from 'react-hot-toast';
import { adminService, type LessonCockpitVideoDto } from '@/services/admin-service';
import { workerService, type WorkerJobStatus } from '@/services/worker-service';
import { resolveMediaUrl } from '@/utils/resolve-media-url';
import SecureVideoPlayer from '@/components/video/SecureVideoPlayer';
import { usePlatformEvents } from '@/hooks/usePlatformEvents';
import { ImageZoomModal } from './ImageZoomModal';
import { ContentInternalCode } from './ContentInternalCode';
import { AdminConfirmationDialog } from './AdminConfirmationDialog';
import { aiJobStatusFromProgressEvent } from '@/lib/ai-job-status';
import { extractApiErrorMessages, getApiErrorSummary } from '@/lib/api-errors';
import { AddVideoForm } from './AddVideoForm';

export function AIProgressTracker({ videoId, isMindmap, onComplete }: { videoId: string, isMindmap?: boolean, onComplete: () => void }) {
  const [status, setStatus] = useState<WorkerJobStatus | null>(null);
  const [isCancelling, setIsCancelling] = useState(false);
  const [isRetrying, setIsRetrying] = useState(false);
  const [statusUnavailable, setStatusUnavailable] = useState(false);
  const [cancelConfirmationOpen, setCancelConfirmationOpen] = useState(false);
  const onCompleteRef = useRef(onComplete);
  const isFinishedRef = useRef(false);

  useEffect(() => {
    onCompleteRef.current = onComplete;
  }, [onComplete]);

  const handleAiJobProgress = (payload: { jobId: string; progress: number; status: string; message: string }) => {
    if (payload.jobId === videoId) {
      setStatusUnavailable(false);
      setStatus(aiJobStatusFromProgressEvent(payload));

      if (payload.progress >= 100) {
        isFinishedRef.current = true;
        setTimeout(() => {
          if (onCompleteRef.current) onCompleteRef.current();
        }, 2000);
      }
    }
  };

  const { isConnected } = usePlatformEvents({
    onAiJobProgress: handleAiJobProgress
  });

  useEffect(() => {
    let timeout: NodeJS.Timeout;

    const checkStatus = async () => {
      if (isCancelling || isFinishedRef.current) return;
      try {
        const workerStatus = await workerService.getWorkerJobStatus(videoId);
        setStatusUnavailable(false);
        setStatus(workerStatus);

        if (workerStatus.state === 'completed' || workerStatus.state === 'not_found') {
          isFinishedRef.current = true;
          timeout = setTimeout(() => {
            if (onCompleteRef.current) onCompleteRef.current();
          }, 2000);
        }
      } catch {
        setStatusUnavailable(true);
      }
    };

    checkStatus();
    const interval = setInterval(checkStatus, isConnected ? 60000 : 30000);

    return () => {
      clearInterval(interval);
      clearTimeout(timeout);
    };
  }, [videoId, isCancelling, isConnected]);

  const handleCancel = async () => {
    setIsCancelling(true);
    try {
      const realId = videoId.replace('_mindmaps', '');

      if (isMindmap) {
        await adminService.cancelMindmapGeneration(realId);
      } else {
        await adminService.cancelVideoAiAnalysis(realId);
      }

      toast.success('تم إلغاء العملية بنجاح');
      onComplete();
    } catch {
      toast.error('تعذر إلغاء العملية');
      setIsCancelling(false);
    }
  };

  const handleRetry = async () => {
    setIsRetrying(true);
    try {
      const realId = videoId.replace('_mindmaps', '');
      if (isMindmap) {
        await adminService.generateVideoMindmaps(realId);
      } else {
        await adminService.triggerVideoAiAnalysis(realId);
      }
      toast.success('تم إعادة تشغيل العملية');
      setStatusUnavailable(false);
      setStatus(null);
    } catch {
      toast.error('تعذر إعادة المحاولة');
    } finally {
      setIsRetrying(false);
    }
  };

  // Derive display values
  const progressVal = status?.progress.percentage ?? 0;
  const progressText = status?.progress.stage
    ? status.progress.stage
    : status?.state === 'waiting'
      ? 'في الطابور...'
      : status?.state === 'completed'
        ? 'اكتملت المعالجة!'
        : 'جاري التحليل والمعالجة...';

  const isFailed = status?.state === 'failed';
  const isCompleted = status?.state === 'completed' || status?.state === 'not_found';
  const isWorking = status?.state === 'active' || status?.state === 'waiting';

  return (
    <div className="flex w-full min-w-0 flex-col items-end gap-1 px-1 py-0.5 sm:w-[260px]">
      {/* Status text + spinner */}
      <div className="flex items-center gap-1.5 font-bold text-[var(--admin-primary)] w-full justify-end">
        {(isWorking || !status) && <Loader2 className="h-3 w-3 animate-spin shrink-0" />}
        {isFailed && <AlertTriangle className="h-3 w-3 shrink-0 text-red-500" />}
        <span
          className={`truncate text-xs ${isFailed ? 'text-red-500' : isCompleted ? 'text-green-500' : 'text-[var(--admin-primary)]'}`}
          title={progressText}
        >
          {isFailed ? 'فشلت العملية' : progressText}
        </span>
      </div>

      {/* Progress bar (when working) */}
      {(isWorking || (!status && !isFailed)) && (
        <div className="w-full h-1 rounded-full overflow-hidden border border-[var(--admin-primary)]/20 bg-[var(--admin-primary)]/10">
          <div
            className="h-full bg-[var(--admin-primary)] transition-[color,background-color,border-color,opacity,transform,box-shadow] duration-[800ms] ease-out"
            style={{ width: `${Math.max(4, progressVal)}%` }}
          />
        </div>
      )}

      {/* Public failure guidance. Technical worker diagnostics never render here. */}
      {isFailed && status.failure && (
        <div
          role="alert"
          dir="rtl"
          className="w-full rounded-lg border border-[var(--admin-danger-20)] bg-[var(--admin-danger-10)] px-2.5 py-2 text-start text-xs leading-5 text-[var(--admin-danger)]"
        >
          {status.failure.message}
        </div>
      )}

      {statusUnavailable && !isFailed && (
        <div
          role="status"
          dir="rtl"
          className="w-full rounded-lg border border-amber-500/25 bg-amber-500/10 px-2.5 py-2 text-start text-xs leading-5 text-amber-700 dark:text-amber-300"
        >
          تعذر تحديث حالة التحليل حاليًا. سنحاول التحقق مرة أخرى تلقائيًا.
        </div>
      )}

      {/* Action buttons */}
      {!isCompleted && (
        <div className="mt-1 flex w-full flex-wrap items-center justify-end gap-2">
          {isFailed && status.failure?.retryable && (
            <button
              type="button"
              onClick={handleRetry}
              disabled={isRetrying || isCancelling}
              className="inline-flex min-h-11 items-center justify-center gap-2 rounded-lg bg-[var(--admin-primary)] px-3 py-2 text-xs font-bold text-white transition hover:opacity-90 disabled:opacity-40"
            >
              <RefreshCw className={`h-3.5 w-3.5 ${isRetrying ? 'animate-spin' : ''}`} />
              {isRetrying ? 'جاري إعادة التشغيل...' : 'إعادة المحاولة'}
            </button>
          )}

          {!isFailed && (
            <button
              type="button"
              onClick={() => setCancelConfirmationOpen(true)}
              disabled={isCancelling || isRetrying}
              title="إيقاف وإلغاء التحليل"
              aria-label="إيقاف وإلغاء التحليل"
              className="flex min-h-11 min-w-11 items-center justify-center rounded-lg bg-[var(--admin-danger-10)] text-[var(--admin-danger)] transition hover:bg-[var(--admin-danger-20)] disabled:opacity-40"
            >
              {isCancelling ? <Loader2 className="h-3.5 w-3.5 animate-spin" /> : <XCircle className="h-3.5 w-3.5" />}
            </button>
          )}
        </div>
      )}
      <AdminConfirmationDialog
        open={cancelConfirmationOpen}
        onClose={() => setCancelConfirmationOpen(false)}
        onConfirm={async () => {
          await handleCancel();
          setCancelConfirmationOpen(false);
        }}
        title="إلغاء التحليل"
        consequence="سيتم إيقاف التحليل أو إنشاء الخريطة الذهنية الجاري الآن. قد لا تُحفظ أي نتائج لم تكتمل بعد."
        confirmLabel="إلغاء العملية"
        variant="danger"
        isConfirming={isCancelling}
      />
    </div>
  );
}

// ── Chapters inline panel ───────────────────────────────────────────────────
function ChaptersInline({ chapters, onRefresh }: { chapters: any[]; onRefresh?: () => void }) {
  const [zoomImage, setZoomImage] = useState<{ url: string; title: string } | null>(null);
  const [regeneratingChapterId, setRegeneratingChapterId] = useState<string | null>(null);

  const handleRegenerateMindmap = async (chapter: any) => {
    if (!chapter?.id) return;
    setRegeneratingChapterId(chapter.id);
    try {
      await adminService.regenerateChapterMindmap(chapter.id);
      toast.success(chapter.mindmapImageUrl ? 'جاري إعادة تصميم صورة الشابتر' : 'جاري توليد صورة الشابتر');
      onRefresh?.();
    } catch (err: any) {
      toast.error(err?.response?.data?.message || 'تعذر تشغيل توليد صورة الشابتر');
    } finally {
      setRegeneratingChapterId(null);
    }
  };

  if (!chapters || chapters.length === 0) {
    return (
      <div className="px-4 pb-3 text-xs text-[var(--admin-muted)]">لا توجد فصول مسجلة لهذا الفيديو</div>
    );
  }
  return (
    <div className="px-4 pb-3 space-y-1">
      {chapters.map((ch: any, i: number) => (
        <div key={ch.id} className="flex items-start gap-2.5 rounded-lg bg-[var(--admin-bg)] border border-[var(--admin-border)] px-3 py-2">
          <div className="flex-shrink-0 w-5 h-5 rounded-full bg-[var(--admin-primary-15)] text-[var(--admin-primary)] text-xs font-bold flex items-center justify-center mt-0.5">{i + 1}</div>
          <div className="flex-1 min-w-0">
            <div className="truncate text-start text-xs font-bold text-[var(--admin-text)]" dir="auto">{ch.title}</div>
            {ch.summaryText && <div className="mt-0.5 line-clamp-2 text-start text-xs text-[var(--admin-muted)]" dir="auto">{ch.summaryText}</div>}
            <div className="mt-2 flex flex-wrap items-center gap-2">
              <button
                type="button"
                onClick={() => handleRegenerateMindmap(ch)}
                disabled={regeneratingChapterId === ch.id}
                className="inline-flex h-8 items-center gap-1.5 rounded-md border border-[var(--admin-primary)]/25 bg-[var(--admin-primary-15)] px-2.5 text-xs font-bold text-[var(--admin-primary)] transition hover:bg-[var(--admin-primary)]/20 disabled:cursor-not-allowed disabled:opacity-60"
                title={ch.mindmapImageUrl ? 'إعادة تصميم صورة هذا الشابتر فقط' : 'توليد صورة لهذا الشابتر فقط'}
              >
                {regeneratingChapterId === ch.id ? <Loader2 className="h-3.5 w-3.5 animate-spin" /> : <Sparkles className="h-3.5 w-3.5" />}
                {ch.mindmapImageUrl ? 'إعادة تصميم' : 'توليد صورة'}
              </button>
            </div>
            {ch.mindmapImageUrl && (
              <div className="mt-2 space-y-1">
                <button
                  type="button"
                  onClick={() => setZoomImage({ url: ch.mindmapImageUrl, title: ch.title })}
                  className="inline-flex items-center gap-1 text-xs text-teal-500 font-bold hover:underline mb-1"
                >
                  <ImageIcon className="w-3.5 h-3.5" />
                  رؤية وتنزيل الخريطة الذهنية
                </button>
                <div
                  onClick={() => setZoomImage({ url: ch.mindmapImageUrl, title: ch.title })}
                  className="cursor-zoom-in relative overflow-hidden rounded border border-[var(--admin-border)] hover:border-teal-500 transition-colors w-fit group max-w-[200px]"
                >
                  <NextImage
                    src={resolveMediaUrl(ch.mindmapImageUrl)}
                    alt={ch.title}
                    width={200}
                    height={112}
                    unoptimized
                    className="h-auto w-full max-w-[200px] transition-transform duration-200 group-hover:scale-[1.03]"
                  />
                  <div className="absolute inset-0 bg-black/20 opacity-0 group-hover:opacity-100 flex items-center justify-center transition-opacity text-white text-sm gap-1 font-bold">
                    <ZoomIn className="w-3.5 h-3.5" />
                    تكبير
                  </div>
                </div>
              </div>
            )}
          </div>
          <div className="flex-shrink-0 text-xs font-mono font-bold text-[var(--admin-primary)] bg-[var(--admin-primary-15)] px-1.5 py-0.5 rounded whitespace-nowrap">
            {Math.floor(ch.startTime / 60)}:{String(ch.startTime % 60).padStart(2, '0')} — {Math.floor(ch.endTime / 60)}:{String(ch.endTime % 60).padStart(2, '0')}
          </div>
        </div>
      ))}

      {zoomImage && (
        <ImageZoomModal
          isOpen={true}
          imageUrl={zoomImage.url}
          title={zoomImage.title}
          onClose={() => setZoomImage(null)}
        />
      )}
    </div>
  );
}

interface LessonVideoListProps {
  videos: LessonCockpitVideoDto[];
  onRefresh?: () => void;
  lessonId: string;
  readOnly?: boolean;
  showProviderDetails?: boolean;
}

function bunnyAssetIsProcessing(status?: string | null) {
  const normalizedStatus = status?.toLowerCase();
  return Boolean(
    normalizedStatus
    && normalizedStatus !== 'ready'
    && normalizedStatus !== 'failed'
    && normalizedStatus !== 'unknown',
  );
}

export function LessonVideoList({ videos, onRefresh, lessonId, readOnly = false, showProviderDetails = true }: LessonVideoListProps) {
  const [triggeringId, setTriggeringId] = useState<string | null>(null);
  const [expandedChapters, setExpandedChapters] = useState<string | null>(null);
  const [deletingId, setDeletingId] = useState<string | null>(null);
  const [videoPendingDeletion, setVideoPendingDeletion] = useState<LessonCockpitVideoDto | null>(null);
  const [previewVideoId, setPreviewVideoId] = useState<string | null>(null);
  const [editingVideoId, setEditingVideoId] = useState<string | null>(null);
  const [togglingActiveId, setTogglingActiveId] = useState<string | null>(null);
  const [bunnyReplacementPendingCancellation, setBunnyReplacementPendingCancellation] = useState<{ assetId: string; videoTitle: string } | null>(null);
  const [cancellingBunnyReplacementId, setCancellingBunnyReplacementId] = useState<string | null>(null);

  const hasPendingBunnyVideo = videos.some((video) => {
    return (video.provider.toLowerCase() === 'bunny' && bunnyAssetIsProcessing(video.bunnyStatus))
      || bunnyAssetIsProcessing(video.pendingBunnyReplacement?.status);
  });

  useEffect(() => {
    if (!hasPendingBunnyVideo || !onRefresh) return;
    const interval = window.setInterval(onRefresh, 15_000);
    return () => window.clearInterval(interval);
  }, [hasPendingBunnyVideo, onRefresh]);

  const toggleChapters = (videoId: string) =>
    setExpandedChapters(prev => prev === videoId ? null : videoId);

  const handleTriggerAI = async (videoId: string) => {
    try {
      setTriggeringId(videoId);
      await adminService.triggerVideoAiAnalysis(videoId);
      toast.success('تم إرسال الفيديو للتحليل بالذكاء الاصطناعي');
      if (onRefresh) onRefresh();
    } catch (err: any) {
      toast.error(err?.response?.data?.message || 'تعذر تشغيل التحليل بالذكاء الاصطناعي');
    } finally {
      setTriggeringId(null);
    }
  };

  const handleTriggerMindmaps = async (videoId: string) => {
    try {
      setTriggeringId(videoId + '_mindmaps');
      await adminService.generateVideoMindmaps(videoId);
      toast.success('تم إرسال الخرائط الذهنية للتوليد');
      if (onRefresh) onRefresh();
    } catch (err: any) {
      toast.error(err?.response?.data?.message || 'تعذر تشغيل المعالجة');
    } finally {
      setTriggeringId(null);
    }
  };

  const startEditVideo = (video: LessonCockpitVideoDto) => {
    setEditingVideoId(video.id);
  };

  const handleToggleActive = async (video: LessonCockpitVideoDto) => {
    try {
      setTogglingActiveId(video.id);
      await adminService.toggleVideoActive(video.id);
      toast.success(video.isActive ? 'تم إخفاء الفيديو عن الطلاب' : 'تم تفعيل الفيديو للطلاب');
      onRefresh?.();
    } catch (err: any) {
      toast.error(err?.response?.data?.message || 'تعذر تغيير حالة الفيديو');
    } finally {
      setTogglingActiveId(null);
    }
  };

  const handleDeleteVideo = async (video: LessonCockpitVideoDto) => {
    try {
      setDeletingId(video.id);
      await adminService.deleteVideo(video.id);
      toast.success('تم حذف الفيديو');
      onRefresh?.();
    } catch (err: any) {
      toast.error(err?.response?.data?.message || 'تعذر حذف الفيديو');
    } finally {
      setDeletingId(null);
    }
  };

  const handleCancelBunnyReplacement = async (assetId: string) => {
    try {
      setCancellingBunnyReplacementId(assetId);
      await adminService.cancelBunnyVideoReplacement(assetId);
      toast.success('تم إلغاء استبدال Bunny. ما زال مصدر الفيديو السابق يعمل.');
      onRefresh?.();
      return true;
    } catch (error: unknown) {
      if (extractApiErrorMessages(error).includes('BUNNY_REPLACEMENT_NOT_PENDING')) {
        toast.success('تم تحديث حالة استبدال Bunny. تحقق من المصدر الحالي للفيديو.');
        onRefresh?.();
        return true;
      }
      toast.error(getApiErrorSummary(error, 'تعذر إلغاء استبدال Bunny'));
      return false;
    } finally {
      setCancellingBunnyReplacementId(null);
    }
  };

  if (!videos || videos.length === 0) {
    return (
      <div className="flex flex-col items-center justify-center rounded-2xl border border-dashed border-[var(--admin-border)] p-12 text-center">
        <div className="mb-4 rounded-full bg-[var(--admin-primary-15)] p-4 text-[var(--admin-primary)]">
          <PlaySquare className="h-8 w-8" />
        </div>
        <h4 className="mb-2 text-lg font-bold text-[var(--admin-text)]">لا يوجد فيديو بعد</h4>
        <p className="max-w-md text-sm text-[var(--admin-muted)] mb-6">
          {readOnly ? 'لم تضف الإدارة فيديوهات لهذه الحصة بعد.' : 'أضف الفيديو الأول من النموذج أدناه لتبدأ في بث محتوى هذه الحصة.'}
        </p>
        {!readOnly && (
          <a
            href="#add-video-form"
            className="inline-flex items-center gap-2 rounded-full bg-[var(--admin-primary)] px-5 py-2.5 text-sm font-bold text-white shadow-sm transition hover:opacity-90"
          >
            + أضف فيديو
          </a>
        )}
      </div>
    );
  }

  return (
    <div className="space-y-3">
      {videos.map((video) => {
        const isGoogleDrive = video.provider === 'google_drive';
        const normalizedBunnyStatus = video.bunnyStatus?.toLowerCase();
        const normalizedPendingReplacementStatus = video.pendingBunnyReplacement?.status.toLowerCase();
        const normalizedLastReplacementOutcomeStatus = video.lastBunnyReplacementOutcome?.status.toLowerCase();
        const bunnyManagedNotReady = video.provider.toLowerCase() === 'bunny'
          && Boolean(normalizedBunnyStatus)
          && normalizedBunnyStatus !== 'ready';
        const chapterCount = video.chapters?.length ?? 0;
        const hasChapters = !isGoogleDrive && chapterCount > 0;

        if (readOnly) {
          return (
            <div
              key={video.id}
              className="rounded-2xl border border-[var(--admin-border)] bg-[var(--admin-card-strong)] p-4 shadow-sm"
            >
              <div className="flex items-center justify-between gap-4">
                <div className="flex min-w-0 items-center gap-3">
                  <div className="flex h-10 w-10 shrink-0 items-center justify-center rounded-xl border border-[var(--admin-border)] bg-[var(--admin-card)] text-[var(--admin-primary)]">
                    <PlaySquare className="h-4 w-4" />
                  </div>
                  <h4 className="truncate text-sm font-black text-[var(--admin-text)]">{video.title}</h4>
                </div>

                <button
                  type="button"
                  aria-label={`معاينة الفيديو ${video.title}`}
                  onClick={() => setPreviewVideoId(video.id)}
                  disabled={bunnyManagedNotReady}
                  className="inline-flex min-h-11 shrink-0 items-center gap-2 rounded-xl bg-[var(--admin-primary)] px-4 py-2 text-sm font-black text-white transition hover:opacity-90 disabled:cursor-not-allowed disabled:opacity-40"
                  title={bunnyManagedNotReady ? 'فيديو Bunny ما زال قيد التجهيز' : 'فتح البلاير'}
                >
                  <Play className="h-4 w-4" />
                  البلاير
                </button>
              </div>
            </div>
          );
        }

        return (
          <div
            key={video.id}
            className={`rounded-xl border border-[var(--admin-border)] bg-[var(--admin-card-strong)] shadow-sm group overflow-hidden transition-[color,background-color,border-color,opacity,transform,box-shadow] ${
              !video.isActive ? 'opacity-60 border-dashed bg-[var(--admin-bg)]' : ''
            }`}
          >
            <div className="flex flex-col sm:flex-row sm:items-center items-start justify-between gap-4 sm:gap-0 p-4">
              <div className="flex items-start sm:items-center gap-3 sm:gap-4 w-full sm:w-auto">
                <div className="flex cursor-grab items-center px-1 text-[var(--admin-muted)] opacity-50 hover:opacity-100">
                  <GripVertical className="h-5 w-5" />
                </div>
                <div className="rounded-lg bg-[var(--admin-card)] p-2.5 text-[var(--admin-text)] border border-[var(--admin-border)]">
                  <PlaySquare className="h-4 w-4" />
                </div>
                <div>
                  <h4 className="font-bold text-[var(--admin-text)]">{video.title}</h4>
                  <div className="mt-2 sm:mt-1 flex flex-wrap items-center gap-2 text-xs sm:text-xs font-mono text-[var(--admin-muted)]">
                    <ContentInternalCode code={video.internalCode} label="كود الفيديو الداخلي" compact />
                    <span className="rounded bg-[var(--admin-primary-15)] px-1.5 py-0.5 font-sans font-bold text-[var(--admin-primary)]">
                      {video.videoType.name}
                    </span>
                    {showProviderDetails && (
                      <span className="rounded bg-[var(--admin-bg)] px-1.5 py-0.5 border border-[var(--admin-border)]">
                        {video.provider === 'google_drive' ? 'Google Drive' : (video.provider || 'YouTube')}
                      </span>
                    )}
                    {video.provider.toLowerCase() === 'bunny' && video.bunnyLibrary && (
                      <span className="rounded border border-[var(--admin-primary)]/20 bg-[var(--admin-primary-15)] px-1.5 py-0.5 font-sans font-bold text-[var(--admin-primary)]">
                        مكتبة: {video.bunnyLibrary.name} · {video.bunnyLibrary.libraryId}
                      </span>
                    )}
                    {video.provider.toLowerCase() === 'bunny' && video.bunnyStatus && (
                      <span className={`rounded border px-1.5 py-0.5 font-sans font-bold ${normalizedBunnyStatus === 'ready'
                        ? 'border-emerald-500/20 bg-emerald-500/10 text-emerald-700 dark:text-emerald-400'
                        : normalizedBunnyStatus === 'failed' || normalizedBunnyStatus === 'unknown'
                          ? 'border-red-500/20 bg-red-500/10 text-red-600 dark:text-red-400'
                          : 'border-amber-500/20 bg-amber-500/10 text-amber-700 dark:text-amber-300'
                      }`}>
                        {normalizedBunnyStatus === 'ready'
                          ? 'Bunny جاهز'
                          : normalizedBunnyStatus === 'failed' || normalizedBunnyStatus === 'unknown'
                            ? 'تعذر تجهيز فيديو Bunny'
                            : `Bunny قيد التجهيز${video.bunnyEncodeProgress != null ? ` · ${video.bunnyEncodeProgress}%` : ''}`}
                      </span>
                    )}
                    {video.pendingBunnyReplacement && (
                      <>
                        <span className={`rounded border px-1.5 py-0.5 font-sans font-bold ${normalizedPendingReplacementStatus === 'failed' || normalizedPendingReplacementStatus === 'unknown'
                          ? 'border-red-500/20 bg-red-500/10 text-red-600 dark:text-red-400'
                          : 'border-amber-500/20 bg-amber-500/10 text-amber-700 dark:text-amber-300'
                        }`}>
                          {normalizedPendingReplacementStatus === 'failed' || normalizedPendingReplacementStatus === 'unknown'
                            ? 'تعذر تجهيز مصدر Bunny الجديد'
                            : `يتم تجهيز مصدر Bunny جديد${video.pendingBunnyReplacement.encodeProgress != null ? ` · ${video.pendingBunnyReplacement.encodeProgress}%` : ''}`}
                        </span>
                        <button
                          type="button"
                          onClick={() => setBunnyReplacementPendingCancellation({
                            assetId: video.pendingBunnyReplacement!.assetId,
                            videoTitle: video.title,
                          })}
                          disabled={cancellingBunnyReplacementId === video.pendingBunnyReplacement.assetId}
                          className="rounded border border-amber-500/25 bg-amber-500/10 px-1.5 py-0.5 font-sans text-xs font-bold text-amber-800 transition-colors hover:bg-amber-500/20 disabled:cursor-not-allowed disabled:opacity-60 dark:text-amber-200"
                        >
                          {cancellingBunnyReplacementId === video.pendingBunnyReplacement.assetId ? 'جارٍ الإلغاء...' : 'إلغاء الاستبدال'}
                        </button>
                      </>
                    )}
                    {!video.pendingBunnyReplacement && video.lastBunnyReplacementOutcome && (
                      <span className={`rounded border px-1.5 py-0.5 font-sans font-bold ${normalizedLastReplacementOutcomeStatus === 'cancelled'
                        ? 'border-[var(--admin-border)] bg-[var(--admin-bg)] text-[var(--admin-muted)]'
                        : 'border-red-500/20 bg-red-500/10 text-red-600 dark:text-red-400'
                      }`}>
                        {normalizedLastReplacementOutcomeStatus === 'cancelled'
                          ? 'تم إلغاء آخر استبدال Bunny؛ المصدر السابق مستمر'
                          : 'لم يكتمل آخر استبدال Bunny؛ المصدر السابق مستمر'}
                      </span>
                    )}
                    <span className="rounded bg-[var(--admin-bg)] px-1.5 py-0.5 border border-[var(--admin-border)]">
                      مشاهدة: {video.maxWatchCount === 0 ? 'غير محدود' : `${video.maxWatchCount}×`}
                    </span>
                    <span className="rounded bg-[var(--admin-bg)] px-1.5 py-0.5 border border-[var(--admin-border)]">
                      ترتيب: {video.order}
                    </span>
                    {(video.examId || (video.exams && video.exams.length > 0)) && (
                      <span className="rounded bg-emerald-500/10 px-1.5 py-0.5 border border-emerald-500/20 text-emerald-600 dark:text-emerald-400 flex items-center gap-1 font-bold">
                        <BookCheck className="h-3 w-3" />
                        امتحان مرفق {video.exams && video.exams.length > 1 ? `(${video.exams.length})` : ''}
                      </span>
                    )}
                    {!video.isActive && (
                      <span className="rounded bg-red-500/10 px-1.5 py-0.5 border border-red-500/20 text-red-600 dark:text-red-400 flex items-center gap-1 font-bold">
                        <EyeOff className="h-3 w-3" />
                        مخفي عن الطلاب
                      </span>
                    )}
                  </div>
                </div>
              </div>

              <div className="flex items-center gap-2 self-end sm:self-auto pt-3 sm:pt-0 w-full sm:w-auto justify-end opacity-60 group-hover:opacity-100 transition-opacity">

                {/* Chapters toggle — shown when video has chapters */}
                {!video.isProcessingAI && !video.isProcessingMindmaps && hasChapters && (
                  <button
                    type="button"
                    onClick={() => toggleChapters(video.id)}
                    className={`flex items-center gap-1 rounded-lg px-2 py-1.5 text-xs font-bold transition-colors ${expandedChapters === video.id
                        ? 'bg-[var(--admin-primary-15)] text-[var(--admin-primary)] border border-[var(--admin-primary)]/30'
                        : 'text-[var(--admin-primary)] hover:bg-[var(--admin-primary-15)] border border-transparent'
                      }`}
                    title={`${chapterCount} فصل — انقر للعرض`}
                  >
                    <BookOpen className="h-3.5 w-3.5" />
                    <span>{chapterCount}</span>
                    <ChevronDown className={`h-3 w-3 transition-transform duration-200 ${expandedChapters === video.id ? 'rotate-180' : ''}`} />
                  </button>
                )}

                {!readOnly && !isGoogleDrive && (
                  <div className="relative group/ai">
                    {video.isProcessingAI ? (
                      <AIProgressTracker videoId={video.id} onComplete={() => onRefresh && onRefresh()} />
                    ) : video.isProcessingMindmaps ? (
                      <AIProgressTracker videoId={video.id + '_mindmaps'} isMindmap onComplete={() => onRefresh && onRefresh()} />
                    ) : (
                      <div className="flex items-center gap-1">
                        {hasChapters && (
                          <button
                            type="button"
                            onClick={() => handleTriggerMindmaps(video.id)}
                            disabled={triggeringId === video.id + '_mindmaps'}
                            className={`rounded-lg p-2 transition-colors ${triggeringId === video.id + '_mindmaps'
                                ? 'text-teal-500/60 opacity-80 cursor-not-allowed animate-pulse bg-teal-500/10'
                                : 'text-teal-500 hover:bg-teal-500/10'
                              }`}
                            aria-label="توليد الخرائط الذهنية"
                            title="توليد الخرائط الذهنية للفصول"
                          >
                            {triggeringId === video.id + '_mindmaps' ? (
                              <Loader2 className="h-4 w-4 animate-spin" />
                            ) : (
                              <Sparkles className="h-4 w-4" />
                            )}
                          </button>
                        )}
                        <button
                          type="button"
                          onClick={() => handleTriggerAI(video.id)}
                          disabled={triggeringId === video.id || triggeringId === video.id + '_mindmaps'}
                          className={`rounded-lg p-2 transition-colors ${triggeringId === video.id
                              ? 'text-[var(--admin-primary)]/60 opacity-50 cursor-not-allowed bg-[var(--admin-primary)]/5'
                              : 'text-[var(--admin-primary)] hover:bg-[var(--admin-primary)]/10'
                            }`}
                          aria-label="استخراج الفصول بالذكاء الاصطناعي"
                          title={chapterCount > 0 ? 'إعادة توليد الفصول' : 'استخراج فصول الفيديو بالذكاء الاصطناعي'}
                        >
                          {triggeringId === video.id ? (
                            <Loader2 className="h-4 w-4 animate-spin" />
                          ) : (
                            <Sparkles className="h-4 w-4" />
                          )}
                        </button>
                      </div>
                    )}
                  </div>
                )}

                <div className="relative group/preview">
                  <button
                    type="button"
                    aria-label="معاينة الفيديو"
                    onClick={() => setPreviewVideoId(video.id)}
                    disabled={bunnyManagedNotReady}
                    className="rounded-lg p-2 text-[var(--admin-primary)] hover:bg-[var(--admin-primary-15)] hover:text-[var(--admin-primary-strong)] transition-colors disabled:cursor-not-allowed disabled:opacity-35"
                    title={bunnyManagedNotReady ? 'انتظر حتى يكتمل تجهيز فيديو Bunny' : 'معاينة الفيديو كطالب'}
                  >
                    <Play className="h-4 w-4" />
                  </button>
                </div>

                {!readOnly && (
                  <>
                    <ContentArchiveControl targetType="Video" targetId={video.id} title={video.title} archiveMode={video.archiveMode} onChanged={onRefresh} compact />
                    <div className="relative group/toggle-active">
                      <button
                        type="button"
                        aria-label={video.isActive ? "إخفاء الفيديو" : "تفعيل الفيديو"}
                        onClick={() => handleToggleActive(video)}
                        disabled={togglingActiveId === video.id || bunnyManagedNotReady}
                        className="rounded-lg p-2 text-[var(--admin-primary)] hover:bg-[var(--admin-primary-15)] hover:text-[var(--admin-primary-strong)] transition-colors disabled:opacity-40"
                        title={bunnyManagedNotReady ? 'يتفعّل تلقائيًا بعد اكتمال تجهيز Bunny' : video.isActive ? "إخفاء الفيديو عن الطلاب" : "تفعيل الفيديو للطلاب"}
                      >
                        {togglingActiveId === video.id ? (
                          <Loader2 className="h-4 w-4 animate-spin" />
                        ) : video.isActive ? (
                          <Eye className="h-4 w-4" />
                        ) : (
                          <EyeOff className="h-4 w-4" />
                        )}
                      </button>
                    </div>

                    <div className="relative group/edit">
                      <button
                        type="button"
                        aria-label="تعديل الفيديو"
                        onClick={() => startEditVideo(video)}
                        disabled={deletingId === video.id}
                        className="rounded-lg p-2 text-[var(--admin-muted)] hover:bg-[var(--admin-bg)] disabled:opacity-40 disabled:cursor-not-allowed"
                      >
                        <Edit2 className="h-4 w-4" />
                      </button>
                    </div>
                    <div className="relative group/del">
                      <button
                        type="button"
                        aria-label="حذف الفيديو"
                        onClick={() => setVideoPendingDeletion(video)}
                        disabled={deletingId === video.id}
                        className="rounded-lg p-2 text-red-500 hover:bg-red-500/10 disabled:opacity-40 disabled:cursor-not-allowed"
                      >
                        {deletingId === video.id ? <Loader2 className="h-4 w-4 animate-spin" /> : <Trash2 className="h-4 w-4" />}
                      </button>
                    </div>
                  </>
                )}
              </div>
            </div>

            {/* Inline Edit Form */}
            {!readOnly && editingVideoId === video.id && (
              <div className="border-t border-[var(--admin-border)] bg-[var(--admin-card)] p-4" dir="rtl">
                <AddVideoForm
                  lessonId={lessonId}
                  editingVideo={video}
                  onCancel={() => setEditingVideoId(null)}
                  onSuccess={() => {
                    setEditingVideoId(null);
                    onRefresh?.();
                  }}
                />
              </div>
            )}{/* end row */}

            {/* Chapters panel */}
            {hasChapters && expandedChapters === video.id && (
              <ChaptersInline chapters={video.chapters ?? []} onRefresh={onRefresh} />
            )}
          </div>
        );
      })}

      {/* Video Preview Modal */}
      {previewVideoId && (
        <div
          className="fixed inset-0 z-[var(--z-modal)] flex items-center justify-center p-4 md:p-8"
          role="dialog"
          aria-modal="true"
          aria-labelledby="lesson-video-preview-title"
        >
          <button
            type="button"
            className="absolute inset-0 bg-black/80 backdrop-blur-md"
            onClick={() => setPreviewVideoId(null)}
            aria-label="إغلاق معاينة الفيديو"
          />
          <div className="relative z-10 bg-[var(--admin-card-strong)] border border-[var(--admin-border)] rounded-2xl overflow-hidden shadow-2xl w-full max-w-4xl flex flex-col">
            <div className="flex items-center justify-between border-b border-[var(--admin-border)] px-6 py-4 bg-[var(--admin-card)]" dir="rtl">
              <h3 id="lesson-video-preview-title" className="text-lg font-bold text-[var(--admin-text)] flex items-center gap-2">
                <Play className="h-5 w-5 text-[var(--admin-primary)]" />
                <span>معاينة الفيديو كطالب: {videos.find(v => v.id === previewVideoId)?.title}</span>
              </h3>
              <button
                type="button"
                onClick={() => setPreviewVideoId(null)}
                className="rounded-full p-1.5 text-[var(--admin-muted)] hover:bg-[var(--admin-bg)] hover:text-[var(--admin-text)] transition-colors"
                aria-label="إغلاق المعاينة"
              >
                <X className="h-5 w-5" />
              </button>
            </div>
            <div className="relative aspect-video w-full bg-black">
              <SecureVideoPlayer lessonVideoId={previewVideoId} />
            </div>
          </div>
        </div>
      )}
      <AdminConfirmationDialog
        open={videoPendingDeletion !== null}
        onClose={() => setVideoPendingDeletion(null)}
        onConfirm={async () => {
          if (!videoPendingDeletion) return;
          await handleDeleteVideo(videoPendingDeletion);
          setVideoPendingDeletion(null);
        }}
        title="حذف الفيديو"
        consequence={`سيُحذف الفيديو «${videoPendingDeletion?.title ?? ''}» نهائيًا من الحصة، ولن يعود متاحًا للطلاب.`}
        confirmLabel="حذف الفيديو نهائيًا"
        variant="danger"
        isConfirming={deletingId === videoPendingDeletion?.id}
      />
      <AdminConfirmationDialog
        open={bunnyReplacementPendingCancellation !== null}
        onClose={() => setBunnyReplacementPendingCancellation(null)}
        onConfirm={async () => {
          if (!bunnyReplacementPendingCancellation) return;
          if (await handleCancelBunnyReplacement(bunnyReplacementPendingCancellation.assetId)) {
            setBunnyReplacementPendingCancellation(null);
          }
        }}
        title="إلغاء استبدال Bunny"
        consequence={`سيبقى الفيديو «${bunnyReplacementPendingCancellation?.videoTitle ?? ''}» على مصدره السابق. لن يُحذف ملف Bunny الذي بدأ تجهيزه تلقائيًا، وقد تحتاج لمراجعته لاحقًا.`}
        confirmLabel="إلغاء الاستبدال"
        isConfirming={cancellingBunnyReplacementId === bunnyReplacementPendingCancellation?.assetId}
      />
    </div>
  );
}
