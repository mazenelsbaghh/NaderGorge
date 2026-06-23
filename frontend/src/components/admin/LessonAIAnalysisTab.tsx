'use client';

import React, { useState } from 'react';
import NextImage from 'next/image';
import { Sparkles, Loader2, Download, AlertTriangle, Eye, EyeOff, Brain, FileVideo, CheckCircle2 } from 'lucide-react';
import toast from 'react-hot-toast';
import { adminService } from '@/services/admin-service';
import { resolveMediaUrl } from '@/utils/resolve-media-url';
import { AIProgressTracker } from './LessonVideoList';
import { ImageZoomModal } from './ImageZoomModal';

interface LessonAIAnalysisTabProps {
  lessonId: string;
  videos: any[];
  onRefresh?: () => void;
}

export function LessonAIAnalysisTab({ videos, onRefresh }: LessonAIAnalysisTabProps) {
  const [triggeringId, setTriggeringId] = useState<string | null>(null);
  const [zoomImage, setZoomImage] = useState<{ url: string; title: string } | null>(null);
  const [isBulkDownloading, setIsBulkDownloading] = useState<string | null>(null);

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
      await adminService.generateVideoMindmaps(videoId);
      toast.success('تم تشغيل توليد الخرائط الذهنية بالذكاء الاصطناعي');
      if (onRefresh) onRefresh();
    } catch {
      toast.error('أخفق تشغيل توليد الخرائط الذهنية');
    } finally {
      setTriggeringId(null);
    }
  };

  const downloadSingleImage = async (imageUrl: string, title: string) => {
    try {
      const resolvedUrl = resolveMediaUrl(imageUrl);
      const response = await fetch(resolvedUrl);
      const blob = await response.blob();
      const blobUrl = window.URL.createObjectURL(blob);
      const a = document.createElement('a');
      a.href = blobUrl;
      const ext = imageUrl.split('.').pop()?.split('?')[0] || 'webp';
      a.download = `${title.replace(/\s+/g, '_')}_mindmap.${ext}`;
      document.body.appendChild(a);
      a.click();
      document.body.removeChild(a);
      window.URL.revokeObjectURL(blobUrl);
    } catch {
      const a = document.createElement('a');
      a.href = resolveMediaUrl(imageUrl);
      a.target = '_blank';
      a.download = `${title}_mindmap`;
      a.click();
    }
  };

  const handleBulkDownload = async (video: any) => {
    const chaptersWithMindmaps = video.chapters?.filter((c: any) => c.mindmapImageUrl) || [];
    if (chaptersWithMindmaps.length === 0) {
      toast.error('لا توجد خرائط ذهنية جاهزة للتنزيل');
      return;
    }

    setIsBulkDownloading(video.id);
    toast.success(`جاري تنزيل ${chaptersWithMindmaps.length} صورة...`);

    try {
      for (let index = 0; index < chaptersWithMindmaps.length; index++) {
        const ch = chaptersWithMindmaps[index];
        await downloadSingleImage(ch.mindmapImageUrl, `${video.title}_فصل_${ch.order}_${ch.title}`);
        // Add a tiny delay between downloads to prevent browser blocking multiple downloads
        await new Promise((resolve) => setTimeout(resolve, 300));
      }
      toast.success('تم تنزيل جميع الخرائط الذهنية بنجاح');
    } catch {
      toast.error('حدث خطأ أثناء التنزيل الجماعي');
    } finally {
      setIsBulkDownloading(null);
    }
  };

  return (
    <div className="space-y-8 animate-in slide-in-from-bottom-2 fade-in duration-200">
      {videos.map((video: any) => {
        const chapters = video.chapters || [];
        const chaptersWithMindmaps = chapters.filter((c: any) => c.mindmapImageUrl) || [];
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
                  <h3 className="text-lg font-bold text-[var(--admin-text)]">{video.title}</h3>
                  <span className="text-[10px] font-mono px-1.5 py-0.5 rounded bg-[var(--admin-card-strong)] text-[var(--admin-muted)] uppercase">
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
                        <AIProgressTracker videoId={video.id} onComplete={() => onRefresh && onRefresh()} />
                      ) : (
                        <AIProgressTracker videoId={video.id + '_mindmaps'} isMindmap onComplete={() => onRefresh && onRefresh()} />
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
                        {hasChapters ? 'إعادة استخراج الفصول والترجمة' : 'تحليل واستخراج الفصول'}
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
                  <p className="text-xs text-[var(--admin-muted)]">ملف الترجمة (SRT)</p>
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
                  <p className="text-xs text-[var(--admin-muted)]">فصول الفيديو</p>
                  <p className="text-sm font-bold mt-1 text-[var(--admin-text)]">
                    {hasChapters ? `تم استخراج ${totalChapters} فصل` : 'لا توجد فصول'}
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
                  <p className="text-xs text-[var(--admin-muted)]">الخرائط الذهنية</p>
                  <p className="text-sm font-bold mt-1 text-[var(--admin-text)]">
                    {mindmapsCount > 0 ? `جاهز ${mindmapsCount} من ${totalChapters}` : 'لم يتم التوليد بعد'}
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
                <div className="flex items-center justify-between">
                  <h4 className="text-sm font-black text-[var(--admin-text)] flex items-center gap-1.5">
                    <Brain className="w-4 h-4 text-teal-500" />
                    الخرائط الذهنية للفصول
                  </h4>

                  {mindmapsCount > 0 && (
                    <button
                      type="button"
                      onClick={() => handleBulkDownload(video)}
                      disabled={isBulkDownloading !== null}
                      className="inline-flex items-center gap-1.5 text-xs text-teal-500 font-bold hover:underline bg-teal-500/5 hover:bg-teal-500/10 px-3 py-1.5 rounded-lg border border-teal-500/25 transition disabled:opacity-50"
                    >
                      {isBulkDownloading === video.id ? (
                        <Loader2 className="w-3.5 h-3.5 animate-spin" />
                      ) : (
                        <Download className="w-3.5 h-3.5" />
                      )}
                      تنزيل جميع الصور دفعة واحدة
                    </button>
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
                          <p className="text-[11px] text-[var(--admin-muted)] line-clamp-2 leading-relaxed">
                            {ch.summaryText}
                          </p>
                        )}
                      </div>

                      {ch.mindmapImageUrl ? (
                        <div className="space-y-3 pt-2">
                          {/* Image preview box */}
                          <div 
                            onClick={() => setZoomImage({ url: ch.mindmapImageUrl, title: ch.title })}
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
                            onClick={() => downloadSingleImage(ch.mindmapImageUrl, `${video.title}_فصل_${ch.order}_${ch.title}`)}
                            className="w-full flex items-center justify-center gap-1.5 rounded-xl bg-[var(--admin-card-strong)] hover:bg-[var(--admin-hover)] border border-[var(--admin-border)] py-2 text-xs font-bold text-[var(--admin-text)] transition"
                          >
                            <Download className="w-3.5 h-3.5 text-[var(--admin-muted)]" />
                            تنزيل الصورة
                          </button>
                        </div>
                      ) : (
                        <div className="flex flex-col items-center justify-center p-6 border border-dashed border-[var(--admin-border)] rounded-xl bg-black/[0.01] text-[var(--admin-muted)] space-y-2 mt-2">
                          <Brain className="w-8 h-8 opacity-25" />
                          <span className="text-[11px] text-center">الخريطة الذهنية لم يتم توليدها بعد</span>
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
