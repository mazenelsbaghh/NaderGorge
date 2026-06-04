'use client';

import { useState } from 'react';
import { adminService } from '@/services/admin-service';
import toast from 'react-hot-toast';
import NeumorphButton from '@/components/ui/neumorph-button';

interface AddResourceFormProps {
  lessonId: string;
  onSuccess?: () => void;
}

export function AddResourceForm({ lessonId, onSuccess }: AddResourceFormProps) {
  const [title, setTitle] = useState('');
  const [fileUrl, setFileUrl] = useState('');
  const [resourceType, setResourceType] = useState('PDF');
  const [saving, setSaving] = useState(false);

  async function handleSubmit(e: React.FormEvent) {
    e.preventDefault();
    if (!title.trim() || !fileUrl.trim()) return;

    try {
      setSaving(true);
      await adminService.createResource({ lessonId, title, fileUrl, resourceType });
      toast.success('تم إرفاق الملف بنجاح.');
      setTitle('');
      setFileUrl('');
      onSuccess?.();
    } catch {
      toast.error('حدث خطأ أثناء إضافة الملف، أعد المحاولة.');
    } finally {
      setSaving(false);
    }
  }

  return (
    <form id="add-resource-form" onSubmit={handleSubmit} className="flex flex-col gap-4">
      <div className="flex flex-wrap items-end gap-4">
        <div className="flex-1 space-y-2 min-w-[200px]">
          <label className="text-xs font-bold text-[var(--admin-muted)]">عنوان المذكرة / الملف</label>
          <input
            type="text"
            value={title}
            onChange={(e) => setTitle(e.target.value)}
            placeholder="مثال: مذكرة الفصل الأول"
            className="w-full rounded-xl border border-[var(--admin-border)] bg-[var(--admin-card)] px-4 py-3 text-sm text-[var(--admin-text)] placeholder-[var(--admin-border)] outline-none focus:border-[var(--admin-primary)] focus:ring-1 focus:ring-[var(--admin-primary)] transition-all"
            required
          />
        </div>
        <div className="w-40 space-y-2">
          <label className="text-xs font-bold text-[var(--admin-muted)]">نوع الملف</label>
          <select
            value={resourceType}
            onChange={(e) => setResourceType(e.target.value)}
            className="w-full rounded-xl border border-[var(--admin-border)] bg-[var(--admin-card)] px-4 py-3 text-sm text-[var(--admin-text)] outline-none focus:border-[var(--admin-primary)] focus:ring-1 focus:ring-[var(--admin-primary)] transition-all"
          >
            <option value="PDF">PDF</option>
            <option value="Document">Word Document</option>
            <option value="Image">Image</option>
            <option value="Other">Other</option>
          </select>
        </div>
        <div className="flex-1 space-y-2 min-w-[200px]">
          <label className="text-xs font-bold text-[var(--admin-muted)]">رابط الملف (URL)</label>
          <input
            type="url"
            value={fileUrl}
            onChange={(e) => setFileUrl(e.target.value)}
            placeholder="https://..."
            className="w-full rounded-xl border border-[var(--admin-border)] bg-[var(--admin-card)] px-4 py-3 text-sm text-[var(--admin-text)] placeholder-[var(--admin-border)] outline-none focus:border-[var(--admin-primary)] focus:ring-1 focus:ring-[var(--admin-primary)] transition-all"
            required
          />
        </div>
        <NeumorphButton
          type="submit"
          disabled={saving || !title.trim() || !fileUrl.trim()}
          loading={saving}
          intent="primary"
          size="lg"
          pill
          className="whitespace-nowrap"
        >
          إضافة الملف
        </NeumorphButton>
      </div>
    </form>
  );
}
