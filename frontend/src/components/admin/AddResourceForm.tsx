'use client';

import { useRef, useState } from 'react';
import axios from 'axios';
import { adminService } from '@/services/admin-service';
import { getApiErrorSummary } from '@/lib/api-errors';
import toast from 'react-hot-toast';
import NeumorphButton from '@/components/ui/neumorph-button';
import { Dropdown } from '@/components/ui/dropdown';

interface AddResourceFormProps {
  lessonId: string;
  onSuccess?: () => void;
}

export function AddResourceForm({ lessonId, onSuccess }: AddResourceFormProps) {
  const [mode, setMode] = useState<'upload' | 'url'>('upload');
  const [title, setTitle] = useState('');
  const [fileUrl, setFileUrl] = useState('');
  const [selectedFile, setSelectedFile] = useState<File | null>(null);
  const [resourceType, setResourceType] = useState('PDF');
  const [saving, setSaving] = useState(false);
  const [uploading, setUploading] = useState(false);
  const fileInputRef = useRef<HTMLInputElement>(null);

  async function handleSubmit(e: React.FormEvent) {
    e.preventDefault();
    if (!title.trim()) return;

    if (mode === 'url' && !fileUrl.trim()) return;
    if (mode === 'upload' && !selectedFile) {
      toast.error('برجاء اختيار ملف للرفع أولاً.');
      return;
    }

    try {
      setSaving(true);
      let finalUrl = fileUrl;

      if (mode === 'upload' && selectedFile) {
        setUploading(true);
        const uploadRes = await adminService.uploadResourceFile(selectedFile);
        if (!uploadRes || !uploadRes.url) {
          throw new Error('تعذر رفع الملف');
        }
        finalUrl = uploadRes.url;
      }

      await adminService.createResource({ lessonId, title, fileUrl: finalUrl, resourceType });
      toast.success('تم إرفاق الملف بنجاح.');
      setTitle('');
      setFileUrl('');
      setSelectedFile(null);
      
      if (fileInputRef.current) fileInputRef.current.value = '';

      onSuccess?.();
    } catch (err: unknown) {
      const timedOut = axios.isAxiosError(err) &&
        (err.code === 'ECONNABORTED' || err.code === 'ETIMEDOUT');
      const tooLarge = axios.isAxiosError(err) && err.response?.status === 413;
      toast.error(timedOut
        ? 'انتهت مهلة رفع أو حفظ الملف. تحقق من قائمة الملفات قبل إعادة المحاولة.'
        : tooLarge
          ? 'حجم الملف أكبر من المسموح. الحد الأقصى 10 ميجابايت.'
          : getApiErrorSummary(err, 'تعذر إضافة الملف. أعد المحاولة.'));
    } finally {
      setSaving(false);
      setUploading(false);
    }
  }

  return (
    <div className="space-y-4">
      {/* Tab toggle */}
      <div className="flex items-center gap-4 border-b border-[var(--admin-border)] pb-2 mb-2" dir="rtl">
        <button
          type="button"
          onClick={() => {
            setMode('upload');
            setFileUrl('');
          }}
          className={`pb-2 px-4 text-sm font-bold border-b-2 transition-colors ${
            mode === 'upload'
              ? 'border-[var(--admin-primary)] text-[var(--admin-primary)]'
              : 'border-transparent text-[var(--admin-muted)] hover:text-[var(--admin-text)]'
          }`}
        >
          رفع ملف مباشر
        </button>
        <button
          type="button"
          onClick={() => {
            setMode('url');
            setSelectedFile(null);
          }}
          className={`pb-2 px-4 text-sm font-bold border-b-2 transition-colors ${
            mode === 'url'
              ? 'border-[var(--admin-primary)] text-[var(--admin-primary)]'
              : 'border-transparent text-[var(--admin-muted)] hover:text-[var(--admin-text)]'
          }`}
        >
          رابط ملف خارجي (URL)
        </button>
      </div>

      <form id="add-resource-form" onSubmit={handleSubmit} className="flex flex-col gap-4">
        <div className="flex flex-wrap items-end gap-4">
          <div className="flex-1 space-y-2 min-w-[200px]">
            <label className="text-xs font-bold text-[var(--admin-muted)]">عنوان المذكرة / الملف</label>
            <input
              type="text"
              value={title}
              onChange={(e) => setTitle(e.target.value)}
              placeholder="مثال: مذكرة الفصل الأول"
              className="w-full rounded-xl border border-[var(--admin-border)] bg-[var(--admin-card)] px-4 py-3 text-sm text-[var(--admin-text)] placeholder-[var(--admin-border)] outline-none focus:border-[var(--admin-primary)] focus:ring-1 focus:ring-[var(--admin-primary)] transition-[color,background-color,border-color,opacity,transform,box-shadow]"
              required
            />
          </div>
          <div className="w-40 space-y-2">
            <Dropdown
              label="نوع الملف"
              value={resourceType}
              onChange={(v) => setResourceType(v as string)}
              size="sm"
              options={[
                { value: 'PDF', label: 'PDF' },
                { value: 'Document', label: 'Word Document' },
                { value: 'Image', label: 'Image' },
                { value: 'Other', label: 'Other' },
              ]}
            />
          </div>

          {mode === 'url' ? (
            <div className="flex-1 space-y-2 min-w-[200px]">
              <label className="text-xs font-bold text-[var(--admin-muted)]">رابط الملف (URL)</label>
              <input
                type="url"
                value={fileUrl}
                onChange={(e) => setFileUrl(e.target.value)}
                placeholder="https://..."
                className="w-full rounded-xl border border-[var(--admin-border)] bg-[var(--admin-card)] px-4 py-3 text-sm text-[var(--admin-text)] placeholder-[var(--admin-border)] outline-none focus:border-[var(--admin-primary)] focus:ring-1 focus:ring-[var(--admin-primary)] transition-[color,background-color,border-color,opacity,transform,box-shadow]"
                required
              />
            </div>
          ) : (
            <div className="flex-1 space-y-2 min-w-[200px]">
              <label className="text-xs font-bold text-[var(--admin-muted)]">اختر الملف للرفع (الحد الأقصى 10 ميجابايت)</label>
              <input
                type="file"
                ref={fileInputRef}
                accept=".pdf,image/*,.doc,.docx,.xls,.xlsx,.zip"
                onChange={(e) => {
                  const file = e.target.files?.[0];
                  setSelectedFile(null);
                  if (file) {
                    if (file.size > 10 * 1024 * 1024) {
                      toast.error('حجم الملف يجب ألا يتجاوز 10 ميجابايت.');
                      e.target.value = '';
                      return;
                    }
                    setSelectedFile(file);
                    if (!title.trim()) {
                      const baseName = file.name.substring(0, file.name.lastIndexOf('.')) || file.name;
                      setTitle(baseName);
                    }
                    const ext = file.name.split('.').pop()?.toLowerCase();
                    if (ext === 'pdf') setResourceType('PDF');
                    else if (['png', 'jpg', 'jpeg', 'webp', 'gif'].includes(ext || '')) setResourceType('Image');
                    else if (['doc', 'docx'].includes(ext || '')) setResourceType('Document');
                    else setResourceType('Other');
                  }
                }}
                className="w-full rounded-xl border border-[var(--admin-border)] bg-[var(--admin-card)] px-4 py-2.5 text-sm text-[var(--admin-text)] file:me-4 file:py-1 file:px-3 file:rounded-full file:border-0 file:text-xs file:font-bold file:bg-[var(--admin-primary-15)] file:text-[var(--admin-primary)] hover:file:bg-[var(--admin-primary)] hover:file:text-white file:transition-colors file:cursor-pointer transition-[color,background-color,border-color,opacity,transform,box-shadow]"
                required
              />
            </div>
          )}

          <NeumorphButton
            type="submit"
            disabled={saving || uploading || !title.trim() || (mode === 'url' ? !fileUrl.trim() : !selectedFile)}
            loading={saving || uploading}
            intent="primary"
            size="lg"
            pill
            className="whitespace-nowrap"
          >
            {uploading ? 'جاري الرفع...' : 'إضافة الملف'}
          </NeumorphButton>
        </div>
      </form>
    </div>
  );
}
