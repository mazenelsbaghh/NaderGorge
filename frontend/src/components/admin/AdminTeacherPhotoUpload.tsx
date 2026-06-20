'use client';
import { useState, useEffect, useCallback } from 'react';
import Image from 'next/image';
import { Upload, Image as ImageIcon, CheckCircle2, Loader2, AlertTriangle, Trash2, Star } from 'lucide-react';
import toast from 'react-hot-toast';
import { adminService } from '@/services/admin-service';
import { useAuthStore } from '@/stores/auth-store';
import { resolveMediaUrl } from '@/utils/resolve-media-url';
import { compressImage, renameFileToMatchBase64 } from '@/utils/image-compressor';

interface AdminTeacherPhotoUploadProps {
  teacherId?: string;
}

export function AdminTeacherPhotoUpload({ teacherId }: AdminTeacherPhotoUploadProps) {
  const { user } = useAuthStore();
  const [photos, setPhotos] = useState<{ id: string; url: string; isActive: boolean; uploadedAt: string }[]>([]);
  const [loadingPhotos, setLoadingPhotos] = useState(false);
  const [isUploading, setIsUploading] = useState(false);

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
    try {
      let successCount = 0;
      for (const file of imageFiles) {
        const base64 = await compressImage(file);
        const finalFileName = renameFileToMatchBase64(file.name, base64);
        
        const res = await adminService.uploadTeacherPhoto(resolvedTeacherId, base64, finalFileName);
        if (res.success) {
          successCount++;
        }
      }
      if (successCount > 0) {
        toast.success(`تم رفع ${successCount} صور بنجاح وتحويلها لصيغة WebP 📸`);
        fetchPhotos();
      } else {
        toast.error('فشل رفع الصور');
      }
    } catch (err: any) {
      toast.error(err.response?.data?.message || 'حدث خطأ أثناء رفع الصور');
    } finally {
      setIsUploading(false);
      if (e.target) e.target.value = '';
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
            onClick={() => document.getElementById('teacher-photos-input')?.click()}
          >
            <input 
              type="file" 
              id="teacher-photos-input" 
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
