'use client';

import { useState } from 'react';
import { Upload, Image as ImageIcon, CheckCircle2, Loader2, AlertTriangle } from 'lucide-react';
import toast from 'react-hot-toast';
import { adminService } from '@/services/admin-service';
import { useAuthStore } from '@/stores/auth-store';

export function AdminTeacherPhotoUpload() {
  const { user } = useAuthStore();
  const [selectedFile, setSelectedFile] = useState<File | null>(null);
  const [preview, setPreview] = useState<string | null>(null);
  const [isUploading, setIsUploading] = useState(false);

  // We assume the logged in admin is the teacher, or at least we use their ID
  const teacherId = user?.id;

  const handleFileChange = (e: React.ChangeEvent<HTMLInputElement>) => {
    const file = e.target.files?.[0];
    if (!file) return;

    if (!file.type.startsWith('image/')) {
      toast.error('الرجاء اختيار صورة صالحة');
      return;
    }

    if (file.size > 5 * 1024 * 1024) {
      toast.error('حجم الصورة يجب أن يكون أقل من 5 ميجابايت');
      return;
    }

    setSelectedFile(file);
    const reader = new FileReader();
    reader.onload = () => setPreview(reader.result as string);
    reader.readAsDataURL(file);
  };

  const handleUpload = async () => {
    if (!selectedFile || !preview || !teacherId) return;

    setIsUploading(true);
    try {
      await adminService.uploadTeacherPhoto(teacherId, preview, selectedFile.name);
      toast.success('تم رفع الصورة بنجاح سيتم استخدامها في الخرائط الذهنية');
      setSelectedFile(null);
      setPreview(null);
    } catch (err: any) {
      toast.error(err.response?.data?.message || 'فشل رفع الصورة');
    } finally {
      setIsUploading(false);
    }
  };

  return (
    <div className="admin-photo-upload bg-[var(--admin-card)] rounded-2xl p-6 border border-[var(--admin-border)] shadow-sm">
      <div className="flex items-center gap-3 mb-4">
        <div className="w-10 h-10 rounded-lg bg-[var(--admin-primary-15)] flex items-center justify-center text-[var(--admin-primary)]">
          <ImageIcon className="w-5 h-5" />
        </div>
        <div>
          <h3 className="text-lg font-bold text-[var(--admin-text)]">صورة الخرائط الذهنية</h3>
          <p className="text-sm text-[var(--admin-muted)]">ارفع صورة ليتم استخدامها كمرجع لِـ Gemini لتحويلها إلى كراكتير للخرائط الذهنية</p>
        </div>
      </div>

      <div className="flex flex-col sm:flex-row gap-6 items-start mt-6">
        <div 
          className={`relative border-2 border-dashed rounded-xl p-8 flex flex-col items-center justify-center flex-1 w-full transition-colors cursor-pointer group ${
            preview ? 'border-[var(--admin-primary)] bg-[var(--admin-primary-15)]/30' : 'border-[var(--admin-border)] hover:border-[var(--admin-primary)] hover:bg-[var(--admin-hover)]'
          }`}
          onClick={() => document.getElementById('teacher-photo-input')?.click()}
        >
          <input 
            type="file" 
            id="teacher-photo-input" 
            accept="image/*" 
            className="hidden" 
            onChange={handleFileChange} 
          />
          
          {preview ? (
            <div className="relative w-32 h-32 rounded-lg overflow-hidden border border-[var(--admin-border)] shadow-md">
              <img src={preview} alt="Preview" className="w-full h-full object-cover" />
              <div className="absolute inset-0 bg-black/40 flex items-center justify-center opacity-0 group-hover:opacity-100 transition-opacity">
                <Upload className="w-6 h-6 text-white" />
              </div>
            </div>
          ) : (
            <div className="flex flex-col items-center gap-2 py-4">
              <div className="w-12 h-12 rounded-full bg-[var(--admin-border)] flex items-center justify-center">
                <Upload className="w-5 h-5 text-[var(--admin-muted)]" />
              </div>
              <span className="text-sm font-semibold text-[var(--admin-text)] mt-2">انقر لاختيار صورة</span>
              <span className="text-xs text-[var(--admin-muted)]">PNG او JPG (الحد الأقصى 5MB)</span>
            </div>
          )}
        </div>

        <div className="flex flex-col gap-4 min-w-[200px] w-full sm:w-auto">
          <div className="text-sm text-[var(--admin-muted)] bg-[var(--admin-hover)] p-3 rounded-lg flex gap-2">
            <AlertTriangle className="w-4 h-4 text-amber-500 shrink-0 mt-0.5" />
            <span>يفضل رفع صورة واضحة الملامح وبدقة عالية للحصول على أفضل جودة للكراكتير.</span>
          </div>

          <button
            onClick={handleUpload}
            disabled={!preview || isUploading || !teacherId}
            className="flex items-center justify-center gap-2 bg-[var(--admin-primary)] text-[var(--admin-text-inverse)] py-2.5 px-4 rounded-xl font-bold transition-transform hover:-translate-y-0.5 disabled:opacity-50 disabled:pointer-events-none"
          >
            {isUploading ? (
              <Loader2 className="w-4 h-4 animate-spin" />
            ) : (
              <CheckCircle2 className="w-4 h-4" />
            )}
            حفظ واستخدام
          </button>
        </div>
      </div>
    </div>
  );
}
