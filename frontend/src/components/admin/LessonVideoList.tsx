'use client';
import { useState, useEffect, useRef } from 'react';
import NextImage from 'next/image';
import { PlaySquare, Trash2, Edit2, GripVertical, Sparkles, Loader2, AlertTriangle, XCircle, RefreshCw, Copy, BookOpen, BookCheck, ChevronDown, Image as ImageIcon, Play, X } from 'lucide-react';
import toast from 'react-hot-toast';
import { adminService } from '@/services/admin-service';
import { workerService, type WorkerJobStatus } from '@/services/worker-service';
import { resolveMediaUrl } from '@/utils/resolve-media-url';
import SecureVideoPlayer from '@/components/video/SecureVideoPlayer';
import { usePlatformEvents } from '@/hooks/usePlatformEvents';

function AIProgressTracker({ videoId, isMindmap, onComplete }: { videoId: string, isMindmap?: boolean, onComplete: () => void }) {
  const [status, setStatus] = useState<WorkerJobStatus | null>(null);
  const [isCancelling, setIsCancelling] = useState(false);
  const [isRetrying, setIsRetrying] = useState(false);
  const onCompleteRef = useRef(onComplete);
  const isFinishedRef = useRef(false);

  useEffect(() => {
    onCompleteRef.current = onComplete;
  }, [onComplete]);

  const handleAiJobProgress = (payload: { jobId: string; progress: number; status: string; message: string }) => {
    if (payload.jobId === videoId) {
      setStatus({
        id: payload.jobId,
        state: payload.progress >= 100 ? 'completed' : payload.progress < 0 ? 'failed' : 'active',
        progress: {
          percentage: payload.progress,
          stage: payload.message || payload.status,
        },
        failedReason: payload.progress < 0 ? payload.message : null,
      } as any);

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
        const data = await workerService.getWorkerJobStatus(videoId);
        setStatus(data);

        if (data.state === 'completed' || data.state === 'not_found') {
          isFinishedRef.current = true;
          timeout = setTimeout(() => {
            if (onCompleteRef.current) onCompleteRef.current();
          }, 2000);
        }
      } catch {
        // silently ignore fetch errors
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
    if (!confirm('هل أنت متأكد من إلغاء العملية؟')) return;
    setIsCancelling(true);
    try {
      await workerService.cancelWorkerJob(videoId);
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
      // If there's an active job, retry it; otherwise re-trigger analysis
      if (status?.state === 'failed') {
        await workerService.retryWorkerJob(videoId);
        toast.success('جاري إعادة المحاولة...');
        setStatus(null);
      } else {
        const realId = videoId.replace('_mindmaps', '');
        if (isMindmap) {
          await adminService.generateVideoMindmaps(realId);
        } else {
          await adminService.triggerVideoAiAnalysis(realId);
        }
        toast.success('تم إعادة تشغيل العملية');
        setStatus(null);
      }
    } catch {
      toast.error('تعذر إعادة المحاولة');
    } finally {
      setIsRetrying(false);
    }
  };

  // Derive display values
  const progressVal = status?.progress
    ? (typeof status.progress === 'object' ? status.progress.percentage : Number(status.progress)) || 0
    : 0;
  const progressText = status?.progress && typeof status.progress === 'object' && status.progress.stage
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
    <div className="flex flex-col gap-1 items-end px-1 py-0.5 min-w-[180px]">
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
            className="h-full bg-[var(--admin-primary)] transition-all duration-[800ms] ease-out"
            style={{ width: `${Math.max(4, progressVal)}%` }}
          />
        </div>
      )}

      {/* Failed reason snippet */}
      {isFailed && status.failedReason && (
        <span className="text-xs text-red-400 truncate w-full" title={status.failedReason}>
          {status.failedReason}
        </span>
      )}

      {/* Action buttons */}
      {!isCompleted && (
        <div className="flex items-center gap-1.5 mt-0.5">
          {/* Copy error (failed only) */}
        {isFailed && (
          <button
            onClick={() => {
              if (status.failedReason) {
                navigator.clipboard.writeText(status.failedReason);
                toast.success('تم نسخ الخطأ');
              }
            }}
            title="نسخ رسالة الخطأ"
            className="flex items-center justify-center h-6 w-6 rounded text-red-400 bg-red-500/10 hover:bg-red-500/20 transition"
          >
            <Copy className="h-3 w-3" />
          </button>
        )}

        {/* Retry button — always shown when processing or failed */}
        <button
          onClick={handleRetry}
          disabled={isRetrying || isCancelling}
          title="إعادة التحليل من البداية"
          className="flex h-8 w-8 items-center justify-center rounded text-[var(--admin-primary)] bg-[var(--admin-primary)]/10 transition hover:bg-[var(--admin-primary)]/20 disabled:opacity-40"
        >
          <RefreshCw className={`h-3 w-3 ${isRetrying ? 'animate-spin' : ''}`} />
        </button>

        {/* Cancel button — always shown */}
        <button
          onClick={handleCancel}
          disabled={isCancelling || isRetrying}
          title="إيقاف وإلغاء التحليل"
          className="flex items-center justify-center h-6 w-6 rounded text-red-500 bg-red-500/10 hover:bg-red-500/20 transition disabled:opacity-40"
        >
          {isCancelling ? <Loader2 className="h-3 w-3 animate-spin" /> : <XCircle className="h-3 w-3" />}
        </button>
      </div>
      )}
    </div>
  );
}

// ── Chapters inline panel ───────────────────────────────────────────────────
function ChaptersInline({ chapters }: { chapters: any[] }) {
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
            <div className="text-xs font-bold text-[var(--admin-text)] truncate">{ch.title}</div>
            {ch.summaryText && <div className="text-xs text-[var(--admin-muted)] mt-0.5 line-clamp-2">{ch.summaryText}</div>}
            {ch.mindmapImageUrl && (
              <div className="mt-2">
                <a href={resolveMediaUrl(ch.mindmapImageUrl)} target="_blank" rel="noreferrer" className="inline-flex items-center gap-1 text-xs text-teal-500 font-bold hover:underline mb-1">
                  <ImageIcon className="w-3 h-3" />
                  رؤية الخريطة الذهنية
                </a>
                <NextImage
                  src={resolveMediaUrl(ch.mindmapImageUrl)}
                  alt={ch.title}
                  width={200}
                  height={112}
                  unoptimized
                  className="h-auto w-full max-w-[200px] rounded border border-[var(--admin-border)]"
                />
              </div>
            )}
          </div>
          <div className="flex-shrink-0 text-xs font-mono font-bold text-[var(--admin-primary)] bg-[var(--admin-primary-15)] px-1.5 py-0.5 rounded whitespace-nowrap">
            {Math.floor(ch.startTime / 60)}:{String(ch.startTime % 60).padStart(2, '0')} — {Math.floor(ch.endTime / 60)}:{String(ch.endTime % 60).padStart(2, '0')}
          </div>
        </div>
      ))}
    </div>
  );
}

interface LessonVideoListProps {
  videos: any[];
  onRefresh?: () => void;
  lessonId: string;
}

export function LessonVideoList({ videos, onRefresh }: LessonVideoListProps) {
  const [triggeringId, setTriggeringId] = useState<string | null>(null);
  const [expandedChapters, setExpandedChapters] = useState<string | null>(null);
  const [updatingId, setUpdatingId] = useState<string | null>(null);
  const [deletingId, setDeletingId] = useState<string | null>(null);
  const [previewVideoId, setPreviewVideoId] = useState<string | null>(null);

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

  const handleEditVideo = async (video: any) => {
    const title = window.prompt('عنوان الفيديو', video.title ?? '');
    if (title === null) return;

    const urlOrEmbedCode = window.prompt('رابط الفيديو أو المعرف', video.providerVideoId ?? '');
    if (urlOrEmbedCode === null) return;

    const orderInput = window.prompt('ترتيب العرض', String(video.order ?? 1));
    if (orderInput === null) return;

    const limitInput = window.prompt('الحد الأقصى للمشاهدات', String(video.maxWatchCount ?? 3));
    if (limitInput === null) return;

    const trimmedTitle = title.trim();
    const trimmedUrl = urlOrEmbedCode.trim();
    const order = Number(orderInput);
    const limit = Number(limitInput);

    if (!trimmedTitle || !trimmedUrl || !Number.isInteger(order) || order < 1 || !Number.isInteger(limit) || limit < 1) {
      toast.error('بيانات التعديل غير صالحة');
      return;
    }

    try {
      setUpdatingId(video.id);
      await adminService.updateVideo(video.id, {
        title: trimmedTitle,
        provider: video.provider,
        urlOrEmbedCode: trimmedUrl,
        order,
        limit,
      });
      toast.success('تم تعديل الفيديو');
      onRefresh?.();
    } catch (err: any) {
      toast.error(err?.response?.data?.message || 'تعذر تعديل الفيديو');
    } finally {
      setUpdatingId(null);
    }
  };

  const handleDeleteVideo = async (video: any) => {
    if (!window.confirm(`حذف الفيديو "${video.title}"؟`)) return;

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

  if (!videos || videos.length === 0) {
    return (
      <div className="flex flex-col items-center justify-center rounded-2xl border border-dashed border-[var(--admin-border)] p-12 text-center">
        <div className="mb-4 rounded-full bg-[var(--admin-primary-15)] p-4 text-[var(--admin-primary)]">
          <PlaySquare className="h-8 w-8" />
        </div>
        <h4 className="mb-2 text-lg font-bold text-[var(--admin-text)]">لا يوجد فيديو بعد</h4>
        <p className="max-w-md text-sm text-[var(--admin-muted)] mb-6">
          أضف الفيديو الأول من النموذج أدناه لتبدأ في بث محتوى هذه الحصة.
        </p>
        <a
          href="#add-video-form"
          className="inline-flex items-center gap-2 rounded-full bg-[var(--admin-primary)] px-5 py-2.5 text-sm font-bold text-white shadow-sm transition hover:opacity-90"
        >
          + أضف فيديو
        </a>
      </div>
    );
  }

  return (
    <div className="space-y-3">
      {videos.map((video) => {
        const isGoogleDrive = video.provider === 'google_drive';
        const hasChapters = !isGoogleDrive && video.chapters && video.chapters.length > 0;

        return (
          <div
            key={video.id}
            className="rounded-xl border border-[var(--admin-border)] bg-[var(--admin-card-strong)] shadow-sm group overflow-hidden"
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
                    <span className="rounded bg-[var(--admin-bg)] px-1.5 py-0.5 border border-[var(--admin-border)]">
                      {video.provider === 'google_drive' ? 'Google Drive' : (video.provider || 'YouTube')}
                    </span>
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
                    title={`${video.chapters.length} فصل — انقر للعرض`}
                  >
                    <BookOpen className="h-3.5 w-3.5" />
                    <span>{video.chapters.length}</span>
                    <ChevronDown className={`h-3 w-3 transition-transform duration-200 ${expandedChapters === video.id ? 'rotate-180' : ''}`} />
                  </button>
                )}

                {!isGoogleDrive && (
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
                          title={video.chapters?.length > 0 ? 'إعادة توليد الفصول' : 'استخراج فصول الفيديو بالذكاء الاصطناعي'}
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
                    className="rounded-lg p-2 text-[var(--admin-primary)] hover:bg-[var(--admin-primary-15)] hover:text-[var(--admin-primary-strong)] transition-colors"
                    title="معاينة الفيديو كطالب"
                  >
                    <Play className="h-4 w-4" />
                  </button>
                </div>
                <div className="relative group/edit">
                  <button
                    type="button"
                    aria-label="تعديل الفيديو"
                    onClick={() => handleEditVideo(video)}
                    disabled={updatingId === video.id || deletingId === video.id}
                    className="rounded-lg p-2 text-[var(--admin-muted)] hover:bg-[var(--admin-bg)] disabled:opacity-40 disabled:cursor-not-allowed"
                  >
                    {updatingId === video.id ? <Loader2 className="h-4 w-4 animate-spin" /> : <Edit2 className="h-4 w-4" />}
                  </button>
                </div>
                <div className="relative group/del">
                  <button
                    type="button"
                    aria-label="حذف الفيديو"
                    onClick={() => handleDeleteVideo(video)}
                    disabled={deletingId === video.id || updatingId === video.id}
                    className="rounded-lg p-2 text-red-500 hover:bg-red-500/10 disabled:opacity-40 disabled:cursor-not-allowed"
                  >
                    {deletingId === video.id ? <Loader2 className="h-4 w-4 animate-spin" /> : <Trash2 className="h-4 w-4" />}
                  </button>
                </div>
              </div>
            </div>{/* end row */}

            {/* Chapters panel */}
            {hasChapters && expandedChapters === video.id && (
              <ChaptersInline chapters={video.chapters ?? []} />
            )}
          </div>
        );
      })}

      {/* Video Preview Modal */}
      {previewVideoId && (
        <div
          className="fixed inset-0 z-[100] flex items-center justify-center p-4 md:p-8"
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
    </div>
  );
}
