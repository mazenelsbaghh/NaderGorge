'use client';
import { useState, useEffect, useCallback, useId } from 'react';
import Image from 'next/image';
import { Upload, Image as ImageIcon, CheckCircle2, Loader2, AlertTriangle, Trash2, Star } from 'lucide-react';
import toast from 'react-hot-toast';
import { adminService } from '@/services/admin-service';
import { useAuthStore } from '@/stores/auth-store';
import { resolveMediaUrl } from '@/utils/resolve-media-url';
import { compressImage, renameFileToMatchBase64 } from '@/utils/image-compressor';

interface AdminTeacherPhotoUploadProps {
  teacherId?: string;
  compact?: boolean;
}

export function AdminTeacherPhotoUpload({ teacherId, compact = false }: AdminTeacherPhotoUploadProps) {
  const { user } = useAuthStore();
  const [photos, setPhotos] = useState<{ id: string; url: string; isActive: boolean; uploadedAt: string }[]>([]);
  const [loadingPhotos, setLoadingPhotos] = useState(false);
  const [isUploading, setIsUploading] = useState(false);
  const inputId = `teacher-photos-${useId().replaceAll(':', '')}`;

  const resolvedTeacherId = teacherId || user?.id;

  const fetchPhotos = useCallback(async () => {
    if (!resolvedTeacherId) {
      setPhotos([]);
      return;
    }
    setLoadingPhotos(true);
    try {
      const res = await adminService.getTeacherPhotos(resolvedTeacherId);
      if (res.success && res.data) {
        setPhotos(res.data);
      }
    } catch (err) {
      console.error('Failed to fetch teacher photos:', err);
    } finally {
      setLoadingPhotos(false);
    }
  }, [resolvedTeacherId]);

  useEffect(() => {
    fetchPhotos();
  }, [fetchPhotos]);

  const handleFilesUpload = async (e: React.ChangeEvent<HTMLInputElement>) => {
    const files = Array.from(e.target.files || []);
    if (files.length === 0 || !resolvedTeacherId) return;

    const imageFiles = files.filter(f => f.type.startsWith('image/'));
    if (imageFiles.length === 0) {
      toast.error('الرجاء اختيار صور صالحة فقط');
      return;
    }

    setIsUploading(true);
    let successCount = 0;
    let failureCount = 0;
    for (const file of imageFiles) {
      try {
        const base64 = await compressImage(file);
        const finalFileName = renameFileToMatchBase64(file.name, base64);
        const res = await adminService.uploadTeacherPhoto(resolvedTeacherId, base64, finalFileName);
        if (res.success) successCount++;
        else failureCount++;
      } catch {
        failureCount++;
      }
    }

    try {
      if (successCount > 0) {
        toast.success(`تم رفع ${successCount} صور بنجاح وتحويلها لصيغة WebP 📸`);
        await fetchPhotos();
      }
      if (failureCount > 0) {
        toast.error(`تعذر رفع ${failureCount} من الصور المحددة`);
      } else if (successCount === 0) {
        toast.error('فشل رفع الصور');
      }
    } finally {
      setIsUploading(false);
      e.target.value = '';
    }
  };

  const handleSetActive = async (photoId: string) => {
    if (!resolvedTeacherId) return;
    try {
      const res = await adminService.setTeacherPhotoActive(resolvedTeacherId, photoId);
      if (res.success) {
        toast.success('تم تحديد الصورة كصورة نشطة بنجاح ✅');
        fetchPhotos();
      } else {
        toast.error(res.message || 'فشل تفعيل الصورة');
      }
    } catch (err) {
      console.error(err);
      toast.error('حدث خطأ أثناء تفعيل الصورة');
    }
  };

  const handleDelete = async (photoId: string) => {
    if (!resolvedTeacherId) return;
    if (!confirm('هل أنت متأكد من حذف هذه الصورة؟')) return;
    try {
      const res = await adminService.deleteTeacherPhoto(resolvedTeacherId, photoId);
      if (res.success) {
        toast.success('تم حذف الصورة بنجاح 🗑️');
        fetchPhotos();
      } else {
        toast.error(res.message || 'فشل حذف الصورة');
      }
    } catch (err) {
      console.error(err);
      toast.error('حدث خطأ أثناء حذف الصورة');
    }
  };

  if (compact) {
    return (
      <div className="space-y-3">
        <div className="flex items-center justify-between gap-3">
          <div className="flex items-center gap-2">
            <ImageIcon className="h-4 w-4 text-[var(--admin-primary)]" />
            <span className="text-xs font-bold text-[var(--admin-text)]">صور التحليل للذكاء الاصطناعي</span>
          </div>
          <span className="text-xs text-[var(--admin-muted)]">{photos.length} صورة</span>
        </div>

        <label
          htmlFor={inputId}
          className="flex min-h-24 cursor-pointer flex-col items-center justify-center rounded-2xl border-2 border-dashed border-[var(--admin-border)] bg-[var(--admin-bg)] p-4 text-center transition hover:border-[var(--admin-primary)]"
        >
          <input
            id={inputId}
            type="file"
            accept="image/*"
            multiple
            className="sr-only"
            onChange={handleFilesUpload}
            disabled={isUploading}
          />
          {isUploading ? (
            <Loader2 className="h-6 w-6 animate-spin text-[var(--admin-primary)]" />
          ) : (
            <Upload className="h-6 w-6 text-[var(--admin-primary)]" />
          )}
          <span className="mt-2 text-xs font-bold text-[var(--admin-text)]">
            {isUploading ? 'جاري ضغط ورفع الصور...' : 'اختر عدة صور للرفع'}
          </span>
          <span className="mt-1 text-[11px] text-[var(--admin-muted)]">يمكن تحديد أكثر من صورة في المرة الواحدة</span>
        </label>

        {loadingPhotos ? (
          <div className="flex min-h-20 items-center justify-center">
            <Loader2 className="h-5 w-5 animate-spin text-[var(--admin-primary)]" />
          </div>
        ) : photos.length > 0 ? (
          <div className="grid grid-cols-2 gap-3 sm:grid-cols-3">
            {photos.map((photo) => (
              <div key={photo.id} className={`rounded-xl border p-2 ${photo.isActive ? 'border-[var(--admin-primary)] bg-[var(--admin-primary-15)]' : 'border-[var(--admin-border)] bg-[var(--admin-bg)]'}`}>
                <div className="relative aspect-square overflow-hidden rounded-lg">
                  <Image src={resolveMediaUrl(photo.url)} alt="صورة مرجعية للمعلم" fill unoptimized className="object-cover" />
                  {photo.isActive && (
                    <span className="absolute right-1 top-1 rounded-md bg-[var(--admin-primary)] px-1.5 py-1 text-[10px] font-bold text-white">النشطة</span>
                  )}
                </div>
                <div className="mt-2 flex justify-center gap-2">
                  {!photo.isActive && (
                    <button type="button" onClick={() => handleSetActive(photo.id)} className="flex min-h-11 min-w-11 items-center justify-center rounded-lg bg-[var(--admin-primary)] text-white" aria-label="تحديد الصورة كمرجع نشط">
                      <Star className="h-4 w-4" />
                    </button>
                  )}
                  <button type="button" onClick={() => handleDelete(photo.id)} className="flex min-h-11 min-w-11 items-center justify-center rounded-lg bg-red-500 text-white" aria-label="حذف الصورة">
                    <Trash2 className="h-4 w-4" />
                  </button>
                </div>
              </div>
            ))}
          </div>
        ) : (
          <p className="text-center text-xs text-[var(--admin-muted)]">لم يتم رفع صور مرجعية بعد.</p>
        )}
      </div>
    );
  }

  return (
    <div className="admin-photo-upload bg-[var(--admin-card)] rounded-2xl p-6 border border-[var(--admin-border)] shadow-sm">
      <div className="flex items-center gap-3 mb-4">
        <div className="w-10 h-10 rounded-lg bg-[var(--admin-primary-15)] flex items-center justify-center text-[var(--admin-primary)]">
          <ImageIcon className="w-5 h-5" />
        </div>
        <div>
          <h3 className="text-lg font-bold text-[var(--admin-text)]">معرض صور المعلم للـ AI</h3>
          <p className="text-sm text-[var(--admin-muted)]">ارفع صورة أو مجموعة صور ليتم استخدامها كمرجع لِـ Gemini لتحويلها إلى كراكتير للخرائط الذهنية</p>
        </div>
      </div>

      <div className="grid grid-cols-1 md:grid-cols-3 gap-6 items-start mt-6" dir="rtl">
        {/* Upload Zone */}
        <div className="md:col-span-1 flex flex-col gap-4">
          <div 
            className="relative border-2 border-dashed rounded-xl p-8 flex flex-col items-center justify-center transition-colors cursor-pointer group border-[var(--admin-border)] hover:border-[var(--admin-primary)] hover:bg-[var(--admin-hover)]"
            onClick={() => document.getElementById(inputId)?.click()}
          >
            <input 
              type="file" 
              id={inputId}
              accept="image/*" 
              multiple
              className="hidden" 
              onChange={handleFilesUpload}
              disabled={isUploading}
            />
            
            {isUploading ? (
              <div className="flex flex-col items-center gap-2 py-4">
                <Loader2 className="w-6 h-6 text-[var(--admin-primary)] animate-spin" />
                <span className="text-xs text-[var(--admin-muted)]">جاري الضغط والرفع...</span>
              </div>
            ) : (
              <div className="flex flex-col items-center gap-2 py-4">
                <div className="w-12 h-12 rounded-full bg-[var(--admin-border)] flex items-center justify-center">
                  <Upload className="w-5 h-5 text-[var(--admin-muted)]" />
                </div>
                <span className="text-sm font-semibold text-[var(--admin-text)] mt-2">اختر صورة أو عدة صور</span>
                <span className="text-xs text-[var(--admin-muted)]">WebP، PNG، JPG (تلقائياً للـ WebP)</span>
              </div>
            )}
          </div>

          <div className="text-sm text-[var(--admin-muted)] bg-[var(--admin-hover)] p-3 rounded-lg flex gap-2">
            <AlertTriangle className="w-4 h-4 text-amber-500 shrink-0 mt-0.5" />
            <span>يمكنك رفع عدة صور وتحديد الصورة النشطة ليستخدمها الذكاء الاصطناعي في رسم الكراكتير.</span>
          </div>
        </div>

        {/* Gallery Zone */}
        <div className="md:col-span-2 space-y-4">
          <h4 className="text-md font-bold text-[var(--admin-text)]">الصور المرفوعة للمعلم ({photos.length})</h4>
          
          {loadingPhotos ? (
            <div className="flex items-center justify-center py-12">
              <Loader2 className="w-8 h-8 text-[var(--admin-primary)] animate-spin" />
            </div>
          ) : photos.length === 0 ? (
            <div className="rounded-xl border border-dashed border-[var(--admin-border)] p-12 text-center text-sm text-[var(--admin-muted)] bg-[var(--admin-bg)]">
              لا توجد صور مرفوعة حالياً. يرجى رفع صورة واحدة على الأقل.
            </div>
          ) : (
            <div className="grid grid-cols-2 sm:grid-cols-3 gap-4">
              {photos.map((photo) => (
                <div 
                  key={photo.id} 
                  className={`relative group rounded-2xl overflow-hidden border-2 bg-[var(--admin-bg)] shadow-sm transition-all ${
                    photo.isActive 
                      ? 'border-[var(--admin-primary)] shadow-md shadow-[var(--admin-primary)]/10 scale-[1.02]' 
                      : 'border-[var(--admin-border)] hover:border-[var(--admin-border-strong)]'
                  }`}
                >
                  <div className="relative aspect-square w-full">
                    <Image 
                      src={resolveMediaUrl(photo.url)} 
                      alt="Teacher Photo" 
                      fill 
                      unoptimized
                      className="object-cover" 
                    />
                    
                    {/* Status Badge */}
                    {photo.isActive && (
                      <div className="absolute top-2 right-2 bg-[var(--admin-primary)] text-white text-[10px] font-bold px-2 py-0.5 rounded-full flex items-center gap-1 shadow-sm">
                        <CheckCircle2 className="w-3 h-3" />
                        نشطة للـ AI
                      </div>
                    )}

                    {/* Action Overlay */}
                    <div className="absolute inset-0 bg-black/60 opacity-0 group-hover:opacity-100 transition-opacity flex items-center justify-center gap-2">
                      {!photo.isActive && (
                        <button
                          type="button"
                          onClick={() => handleSetActive(photo.id)}
                          className="bg-[var(--admin-primary)] text-white p-2 rounded-xl hover:scale-105 transition-transform shadow-md"
                          title="تحديد كصورة نشطة"
                        >
                          <Star className="w-4 h-4 fill-white" />
                        </button>
                      )}
                      <button
                        type="button"
                        onClick={() => handleDelete(photo.id)}
                        className="bg-red-500 text-white p-2 rounded-xl hover:scale-105 transition-transform shadow-md"
                        title="حذف الصورة"
                      >
                        <Trash2 className="w-4 h-4" />
                      </button>
                    </div>
                  </div>
                </div>
              ))}
            </div>
          )}
        </div>
      </div>
    </div>
  );
}
