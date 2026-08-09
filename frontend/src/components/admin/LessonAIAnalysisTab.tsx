'use client';

import React, { useEffect, useState } from 'react';
import NextImage from 'next/image';
import {
  Sparkles,
  Loader2,
  Download,
  AlertTriangle,
  Eye,
  EyeOff,
  Brain,
  FileVideo,
  CheckCircle2,
  FileArchive,
  FileText,
  Palette,
  Shuffle,
  UserRound,
} from 'lucide-react';
import toast from 'react-hot-toast';
import {
  adminService,
  type MindmapStyleSelection,
} from '@/services/admin-service';
import { resolveMediaUrl } from '@/utils/resolve-media-url';
import { AIProgressTracker } from './LessonVideoList';
import { ImageZoomModal } from './ImageZoomModal';
import {
  downloadMindmap,
  downloadMindmapsPdf,
  downloadMindmapsZip,
  type DownloadableMindmap,
} from '@/utils/mindmap-downloads';

interface LessonAIAnalysisTabProps {
  lessonId: string;
  videos: any[];
  onRefresh?: () => void;
}

const VISUAL_STYLES = [
  { id: 'random', label: 'عشوائي لكل صورة' },
  { id: 'editorial-infographic', label: 'إنفوجرافيك' },
  { id: 'cinematic-3d', label: 'سينمائي 3D' },
  { id: 'scientific-notebook', label: 'كراسة علمية' },
  { id: 'museum-exhibit', label: 'معرض متحفي' },
  { id: 'motion-poster', label: 'بوستر حديث' },
];

const TEACHER_STYLES = [
  { id: 'random', label: 'عشوائي لكل صورة' },
  { id: 'photorealistic', label: 'واقعي' },
  { id: 'cartoon', label: 'كرتوني' },
  { id: '3d-character', label: 'شخصية 3D' },
  { id: 'digital-illustration', label: 'رسم رقمي' },
];

function toggleStyle(styles: string[], style: string): string[] {
  if (style === 'random') return ['random'];
  if (styles.includes('random')) return [style];
  if (styles.includes(style))
    return styles.length === 1
      ? styles
      : styles.filter((current) => current !== style);
  return styles.length === 3 ? styles : [...styles, style];
}

export function LessonAIAnalysisTab({
  videos,
  onRefresh,
}: LessonAIAnalysisTabProps) {
  const [triggeringId, setTriggeringId] = useState<string | null>(null);
  const [zoomImage, setZoomImage] = useState<{
    url: string;
    title: string;
  } | null>(null);
  const [bulkDownload, setBulkDownload] = useState<{
    videoId: string;
    format: 'zip' | 'pdf';
  } | null>(null);
  const [regeneratingChapterId, setRegeneratingChapterId] = useState<
    string | null
  >(null);
  const [styles, setStyles] = useState<MindmapStyleSelection>({
    visualStyles: ['random'],
    teacherStyles: ['random'],
  });
  const hasRegeneratingChapter = videos.some((video) =>
    video.chapters?.some((chapter: any) => chapter.isRegeneratingMindmap)
  );

  useEffect(() => {
    if (!hasRegeneratingChapter || !onRefresh) return;

    const refreshTimer = window.setInterval(onRefresh, 3_000);
    return () => window.clearInterval(refreshTimer);
  }, [hasRegeneratingChapter, onRefresh]);

  if (!videos || videos.length === 0) {
    return (
      <div className="rounded-3xl border border-[var(--admin-border)] bg-[var(--admin-card)] p-8 text-center text-[var(--admin-muted)]">
        لا توجد فيديوهات مرفقة بهذه الحصة لبدء تحليل الذكاء الاصطناعي.
      </div>
    );
  }

  const handleTriggerAI = async (videoId: string) => {
    setTriggeringId(videoId);
    try {
      await adminService.triggerVideoAiAnalysis(videoId);
      toast.success('تم تشغيل استخراج الفصول والترجمة بالذكاء الاصطناعي');
      if (onRefresh) onRefresh();
    } catch {
      toast.error('أخفق تشغيل تحليل الفيديو');
    } finally {
      setTriggeringId(null);
    }
  };

  const handleTriggerMindmaps = async (videoId: string) => {
    setTriggeringId(videoId + '_mindmaps');
    try {
      await adminService.generateVideoMindmaps(videoId, styles);
      toast.success('تم تشغيل توليد الخرائط الذهنية بالذكاء الاصطناعي');
      if (onRefresh) onRefresh();
    } catch {
      toast.error('أخفق تشغيل توليد الخرائط الذهنية');
    } finally {
      setTriggeringId(null);
    }
  };

  const handleRegenerateChapterMindmap = async (chapter: any) => {
    if (!chapter?.id) return;

    setRegeneratingChapterId(chapter.id);
    try {
      await adminService.regenerateChapterMindmap(chapter.id, styles);
      onRefresh?.();
      toast.success(
        chapter.mindmapImageUrl
          ? 'جاري إعادة تصميم صورة الشابتر'
          : 'جاري توليد صورة الشابتر'
      );
    } catch (err: any) {
      toast.error(
        err?.response?.data?.message || 'تعذر تشغيل توليد صورة الشابتر'
      );
    } finally {
      setRegeneratingChapterId(null);
    }
  };

  const downloadSingleImage = async (imageUrl: string, title: string) => {
    try {
      await downloadMindmap(imageUrl, `${title}_mindmap`);
    } catch {
      toast.error('تعذر تنزيل الصورة. حاول مرة أخرى.');
    }
  };

  const handleBulkDownload = async (video: any, format: 'zip' | 'pdf') => {
    const chaptersWithMindmaps =
      video.chapters?.filter((c: any) => c.mindmapImageUrl) || [];
    if (chaptersWithMindmaps.length === 0) {
      toast.error('لا توجد خرائط ذهنية جاهزة للتنزيل');
      return;
    }

    const mindmaps: DownloadableMindmap[] = chaptersWithMindmaps.map(
      (chapter: any) => ({
        imageUrl: chapter.mindmapImageUrl,
        fileName: `${String(chapter.order).padStart(2, '0')}_${chapter.title}`,
      })
    );
    setBulkDownload({ videoId: video.id, format });

    try {
      if (format === 'zip') await downloadMindmapsZip(video.title, mindmaps);
      else await downloadMindmapsPdf(video.title, mindmaps);
      toast.success(
        format === 'zip' ? 'تم تنزيل الصور في ملف واحد' : 'تم تنزيل ملف PDF'
      );
    } catch {
      toast.error('تعذر تجهيز الملف. تأكد من اتصالك وحاول مرة أخرى.');
    } finally {
      setBulkDownload(null);
    }
  };

  return (
    <div className="space-y-8 animate-in slide-in-from-bottom-2 fade-in duration-200">
      <section
        className="rounded-2xl border border-[var(--admin-border)] bg-[var(--admin-card)] p-5"
        aria-labelledby="mindmap-style-title"
      >
        <div className="mb-4">
          <h3
            id="mindmap-style-title"
            className="font-black text-[var(--admin-text)]"
          >
            شكل صور تحليل AI
          </h3>
          <p className="mt-1 text-xs leading-5 text-[var(--admin-muted)]">
            اختار حتى 3 اختيارات من كل مجموعة. سيتم استخدام كل صور المدرس مع
            تثبيت ملامحه، ويسمح فقط بتغيير الملابس والوضعية والخلفية.
          </p>
          {(styles.visualStyles.includes('random') ||
            styles.teacherStyles.includes('random')) && (
            <p className="mt-2 inline-flex items-center gap-1.5 text-xs font-bold text-teal-700">
              <Shuffle className="h-3.5 w-3.5" />
              العشوائي يختار شكلًا مختلفًا لكل صورة عند توليدها.
            </p>
          )}
        </div>
        <div className="grid gap-5 lg:grid-cols-2">
          <StyleChoices
            icon={Palette}
            label="ستايل التصميم"
            choices={VISUAL_STYLES}
            selected={styles.visualStyles}
            onToggle={(style) =>
              setStyles((current) => ({
                ...current,
                visualStyles: toggleStyle(current.visualStyles, style),
              }))
            }
          />
          <StyleChoices
            icon={UserRound}
            label="شكل ظهور المدرس"
            choices={TEACHER_STYLES}
            selected={styles.teacherStyles}
            onToggle={(style) =>
              setStyles((current) => ({
                ...current,
                teacherStyles: toggleStyle(current.teacherStyles, style),
              }))
            }
          />
        </div>
      </section>
      {videos.map((video: any) => {
        const chapters = video.chapters || [];
        const chaptersWithMindmaps =
          chapters.filter((c: any) => c.mindmapImageUrl) || [];
        const hasChapters = chapters.length > 0;
        const totalChapters = chapters.length;
        const mindmapsCount = chaptersWithMindmaps.length;

        const isProcessing = video.isProcessingAI || video.isProcessingMindmaps;
        const isGoogleDrive = video.provider?.toLowerCase() === 'googledrive';

        return (
          <div
            key={video.id}
            className="rounded-3xl border border-[var(--admin-border)] bg-[var(--admin-card)] p-6 shadow-sm space-y-6"
          >
            {/* Header Info */}
            <div className="flex flex-col sm:flex-row sm:items-center justify-between gap-4 pb-4 border-b border-[var(--admin-border)]">
              <div className="space-y-1">
                <div className="flex items-center gap-2">
                  <FileVideo className="w-5 h-5 text-[var(--admin-primary)]" />
                  <h3 className="text-lg font-bold text-[var(--admin-text)]">
                    {video.title}
                  </h3>
                  <span className="text-sm font-mono px-1.5 py-0.5 rounded bg-[var(--admin-card-strong)] text-[var(--admin-muted)] uppercase">
                    {video.provider}
                  </span>
                </div>
                <div className="flex items-center gap-3 text-xs text-[var(--admin-muted)]">
                  <span>ترتيب العرض: {video.order}</span>
                  <span>•</span>
                  <span>الحد الأقصى للمشاهدة: {video.maxWatchCount}</span>
                  <span>•</span>
                  {video.isActive ? (
                    <span className="text-emerald-500 font-bold flex items-center gap-0.5">
                      <Eye className="w-3.5 h-3.5" /> مرئي للطلاب
                    </span>
                  ) : (
                    <span className="text-red-500 font-bold flex items-center gap-0.5">
                      <EyeOff className="w-3.5 h-3.5" /> مخفي عن الطلاب
                    </span>
                  )}
                </div>
              </div>

              {/* Top Action Triggers */}
              {!isGoogleDrive && (
                <div className="flex flex-wrap items-center gap-2">
                  {isProcessing ? (
                    <div className="bg-[var(--admin-card-soft)] px-4 py-2 rounded-2xl border border-[var(--admin-border)]">
                      {video.isProcessingAI ? (
                        <AIProgressTracker
                          videoId={video.id}
                          onComplete={() => onRefresh && onRefresh()}
                        />
                      ) : (
                        <AIProgressTracker
                          videoId={video.id + '_mindmaps'}
                          isMindmap
                          onComplete={() => onRefresh && onRefresh()}
                        />
                      )}
                    </div>
                  ) : (
                    <>
                      {hasChapters && (
                        <button
                          type="button"
                          onClick={() => handleTriggerMindmaps(video.id)}
                          disabled={triggeringId !== null}
                          className="inline-flex items-center gap-2 rounded-xl bg-teal-500 hover:bg-teal-600 text-white px-4 py-2.5 text-xs font-bold transition disabled:opacity-50"
                        >
                          {triggeringId === video.id + '_mindmaps' ? (
                            <Loader2 className="w-4 h-4 animate-spin" />
                          ) : (
                            <Brain className="w-4 h-4" />
                          )}
                          توليد الخرائط الذهنية
                        </button>
                      )}

                      <button
                        type="button"
                        onClick={() => handleTriggerAI(video.id)}
                        disabled={triggeringId !== null}
                        className="inline-flex items-center gap-2 rounded-xl bg-[var(--admin-primary)] hover:bg-[var(--admin-primary-strong)] text-white px-4 py-2.5 text-xs font-bold transition disabled:opacity-50"
                      >
                        {triggeringId === video.id ? (
                          <Loader2 className="w-4 h-4 animate-spin" />
                        ) : (
                          <Sparkles className="w-4 h-4" />
                        )}
                        {hasChapters
                          ? 'إعادة استخراج الفصول والترجمة'
                          : 'تحليل واستخراج الفصول'}
                      </button>
                    </>
                  )}
                </div>
              )}
            </div>

            {/* AI Status Dashboard */}
            <div className="grid grid-cols-1 md:grid-cols-3 gap-4">
              <div className="p-4 rounded-2xl bg-[var(--admin-card-soft)] border border-[var(--admin-border)] flex items-center justify-between">
                <div>
                  <p className="text-xs text-[var(--admin-muted)]">
                    ملف الترجمة (SRT)
                  </p>
                  <p className="text-sm font-bold mt-1 text-[var(--admin-text)]">
                    {video.subtitleUrl ? 'متوفر وجاهز' : 'غير متوفر'}
                  </p>
                </div>
                {video.subtitleUrl ? (
                  <CheckCircle2 className="w-8 h-8 text-emerald-500 opacity-80" />
                ) : (
                  <AlertTriangle className="w-8 h-8 text-amber-500 opacity-60" />
                )}
              </div>

              <div className="p-4 rounded-2xl bg-[var(--admin-card-soft)] border border-[var(--admin-border)] flex items-center justify-between">
                <div>
                  <p className="text-xs text-[var(--admin-muted)]">
                    فصول الفيديو
                  </p>
                  <p className="text-sm font-bold mt-1 text-[var(--admin-text)]">
                    {hasChapters
                      ? `تم استخراج ${totalChapters} فصل`
                      : 'لا توجد فصول'}
                  </p>
                </div>
                {hasChapters ? (
                  <CheckCircle2 className="w-8 h-8 text-emerald-500 opacity-80" />
                ) : (
                  <AlertTriangle className="w-8 h-8 text-amber-500 opacity-60" />
                )}
              </div>

              <div className="p-4 rounded-2xl bg-[var(--admin-card-soft)] border border-[var(--admin-border)] flex items-center justify-between">
                <div>
                  <p className="text-xs text-[var(--admin-muted)]">
                    الخرائط الذهنية
                  </p>
                  <p className="text-sm font-bold mt-1 text-[var(--admin-text)]">
                    {mindmapsCount > 0
                      ? `جاهز ${mindmapsCount} من ${totalChapters}`
                      : 'لم يتم التوليد بعد'}
                  </p>
                </div>
                {mindmapsCount === totalChapters && totalChapters > 0 ? (
                  <CheckCircle2 className="w-8 h-8 text-emerald-500 opacity-80" />
                ) : mindmapsCount > 0 ? (
                  <Loader2 className="w-8 h-8 text-teal-500 animate-spin opacity-80" />
                ) : (
                  <AlertTriangle className="w-8 h-8 text-amber-500 opacity-60" />
                )}
              </div>
            </div>

            {/* Generated Mindmaps Section */}
            {hasChapters && (
              <div className="space-y-4 pt-4">
                <div className="flex flex-col gap-3 sm:flex-row sm:items-center sm:justify-between">
                  <h4 className="text-sm font-black text-[var(--admin-text)] flex items-center gap-1.5">
                    <Brain className="w-4 h-4 text-teal-500" />
                    الخرائط الذهنية للفصول
                  </h4>

                  {mindmapsCount > 0 && (
                    <div
                      className="flex flex-wrap items-center gap-2"
                      aria-label="تنزيل الخرائط الذهنية"
                    >
                      <button
                        type="button"
                        onClick={() => handleBulkDownload(video, 'zip')}
                        disabled={bulkDownload !== null}
                        className="inline-flex min-h-11 items-center gap-2 rounded-xl bg-teal-600 px-3.5 text-xs font-bold text-white transition-colors hover:bg-teal-700 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-teal-500 focus-visible:ring-offset-2 disabled:cursor-wait disabled:opacity-60"
                      >
                        {bulkDownload?.videoId === video.id &&
                        bulkDownload?.format === 'zip' ? (
                          <Loader2 className="h-4 w-4 animate-spin" />
                        ) : (
                          <FileArchive className="h-4 w-4" />
                        )}
                        تنزيل الصور ZIP
                      </button>
                      <button
                        type="button"
                        onClick={() => handleBulkDownload(video, 'pdf')}
                        disabled={bulkDownload !== null}
                        className="inline-flex min-h-11 items-center gap-2 rounded-xl border border-[var(--admin-border)] bg-[var(--admin-card)] px-3.5 text-xs font-bold text-[var(--admin-text)] transition-colors hover:bg-[var(--admin-hover)] focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-teal-500 focus-visible:ring-offset-2 disabled:cursor-wait disabled:opacity-60"
                      >
                        {bulkDownload?.videoId === video.id &&
                        bulkDownload?.format === 'pdf' ? (
                          <Loader2 className="h-4 w-4 animate-spin" />
                        ) : (
                          <FileText className="h-4 w-4 text-teal-600" />
                        )}
                        تنزيل PDF واحد
                      </button>
                    </div>
                  )}
                </div>

                <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-4">
                  {chapters.map((ch: any) => (
                    <div
                      key={ch.id}
                      className="rounded-2xl border border-[var(--admin-border)] bg-[var(--admin-card-soft)] p-4 space-y-3 flex flex-col justify-between"
                    >
                      <div className="space-y-2">
                        <div className="flex items-center gap-2">
                          <span className="w-5 h-5 rounded-full bg-[var(--admin-primary-15)] text-[var(--admin-primary)] text-xs font-bold flex items-center justify-center">
                            {ch.order}
                          </span>
                          <span className="text-xs font-bold text-[var(--admin-text)] truncate flex-1">
                            {ch.title}
                          </span>
                        </div>
                        {ch.summaryText && (
                          <p className="text-sm text-[var(--admin-muted)] line-clamp-2 leading-relaxed">
                            {ch.summaryText}
                          </p>
                        )}
                      </div>

                      <button
                        type="button"
                        onClick={() => handleRegenerateChapterMindmap(ch)}
                        disabled={
                          regeneratingChapterId === ch.id ||
                          ch.isRegeneratingMindmap
                        }
                        className="inline-flex h-9 w-full items-center justify-center gap-1.5 rounded-xl border border-[var(--admin-primary)]/25 bg-[var(--admin-primary-15)] px-3 text-xs font-bold text-[var(--admin-primary)] transition hover:bg-[var(--admin-primary)]/20 disabled:cursor-wait disabled:opacity-70"
                        title={
                          ch.mindmapImageUrl
                            ? 'إعادة تصميم صورة هذا الشابتر فقط'
                            : 'توليد صورة لهذا الشابتر فقط'
                        }
                      >
                        {regeneratingChapterId === ch.id ||
                        ch.isRegeneratingMindmap ? (
                          <Loader2 className="h-3.5 w-3.5 animate-spin" />
                        ) : (
                          <Sparkles className="h-3.5 w-3.5" />
                        )}
                        {regeneratingChapterId === ch.id ||
                        ch.isRegeneratingMindmap
                          ? 'جاري إعادة توليد الصورة...'
                          : ch.mindmapImageUrl
                          ? 'إعادة تصميم صورة الشابتر'
                          : 'توليد صورة الشابتر'}
                      </button>

                      {ch.mindmapImageUrl ? (
                        <div className="space-y-3 pt-2">
                          {/* Image preview box */}
                          <div
                            onClick={() =>
                              setZoomImage({
                                url: ch.mindmapImageUrl,
                                title: ch.title,
                              })
                            }
                            className="cursor-zoom-in relative aspect-video overflow-hidden rounded-xl border border-[var(--admin-border)] bg-black/5 hover:border-teal-500 transition duration-200 group"
                          >
                            <NextImage
                              src={resolveMediaUrl(ch.mindmapImageUrl)}
                              alt={ch.title}
                              fill
                              unoptimized
                              className="object-cover transition duration-300 group-hover:scale-105"
                            />
                            <div className="absolute inset-0 bg-black/40 opacity-0 group-hover:opacity-100 flex items-center justify-center transition-opacity text-white text-xs font-bold">
                              انقر للتكبير والتنزيل
                            </div>
                          </div>

                          <button
                            type="button"
                            onClick={() =>
                              downloadSingleImage(
                                ch.mindmapImageUrl,
                                `${video.title}_فصل_${ch.order}_${ch.title}`
                              )
                            }
                            className="w-full flex items-center justify-center gap-1.5 rounded-xl bg-[var(--admin-card-strong)] hover:bg-[var(--admin-hover)] border border-[var(--admin-border)] py-2 text-xs font-bold text-[var(--admin-text)] transition"
                          >
                            <Download className="w-3.5 h-3.5 text-[var(--admin-muted)]" />
                            تنزيل الصورة
                          </button>
                        </div>
                      ) : (
                        <div className="flex flex-col items-center justify-center p-6 border border-dashed border-[var(--admin-border)] rounded-xl bg-black/[0.01] text-[var(--admin-muted)] space-y-2 mt-2">
                          <Brain className="w-8 h-8 opacity-25" />
                          <span className="text-sm text-center">
                            الخريطة الذهنية لم يتم توليدها بعد
                          </span>
                        </div>
                      )}
                    </div>
                  ))}
                </div>
              </div>
            )}
          </div>
        );
      })}

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

function StyleChoices({
  icon: Icon,
  label,
  choices,
  selected,
  onToggle,
}: {
  icon: React.ComponentType<{ className?: string }>;
  label: string;
  choices: Array<{ id: string; label: string }>;
  selected: string[];
  onToggle: (style: string) => void;
}) {
  return (
    <fieldset>
      <legend className="mb-2 flex items-center gap-2 text-sm font-bold text-[var(--admin-text)]">
        <Icon className="h-4 w-4 text-teal-600" />
        {label}
      </legend>
      <div className="flex flex-wrap gap-2">
        {choices.map((choice) => {
          const isSelected = selected.includes(choice.id);
          return (
            <button
              key={choice.id}
              type="button"
              aria-pressed={isSelected}
              onClick={() => onToggle(choice.id)}
              className={`min-h-11 rounded-xl border px-3 text-xs font-bold transition-colors focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-teal-500 ${isSelected ? 'border-teal-600 bg-teal-600 text-white' : 'border-[var(--admin-border)] bg-[var(--admin-card-soft)] text-[var(--admin-text)] hover:border-teal-500'}`}
            >
              {choice.label}
            </button>
          );
        })}
      </div>
    </fieldset>
  );
}
