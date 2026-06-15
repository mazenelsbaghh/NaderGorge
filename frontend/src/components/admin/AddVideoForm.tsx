'use client';

import { useState } from 'react';
import { adminService } from '@/services/admin-service';
import toast from 'react-hot-toast';
import { NumberField } from '@/components/ui/number-field';
import NeumorphButton from '@/components/ui/neumorph-button';
import { Dropdown } from '@/components/ui/dropdown';

interface AddVideoFormProps {
  lessonId: string;
  onSuccess?: () => void;
}

export function AddVideoForm({ lessonId, onSuccess }: AddVideoFormProps) {
  const [title, setTitle] = useState('');
  const [provider, setProvider] = useState('YouTube');
  const [urlOrEmbedCode, setUrlOrEmbedCode] = useState('');
  const [order, setOrder] = useState(1);
  const [limit, setLimit] = useState(3);
  const [isActive, setIsActive] = useState(true);
  const [saving, setSaving] = useState(false);

  async function handleSubmit(e: React.FormEvent) {
    e.preventDefault();
    if (!title.trim() || !urlOrEmbedCode.trim()) return;

    try {
      setSaving(true);
      await adminService.createVideo({ lessonId, title, provider, urlOrEmbedCode, order, limit, isActive });
      toast.success('تمت إضافة الفيديو بنجاح.');
      setTitle('');
      setUrlOrEmbedCode('');
      setOrder((prev) => prev + 1);
      onSuccess?.();
    } catch {
      toast.error('حدث خطأ أثناء إضافة الفيديو، أعد المحاولة.');
    } finally {
      setSaving(false);
    }
  }

  return (
    <form id="add-video-form" onSubmit={handleSubmit} className="flex flex-col gap-4">
      <div className="flex flex-wrap items-end gap-4">
        <div className="flex-1 space-y-2 min-w-[200px]">
          <label className="text-xs font-bold text-[var(--admin-muted)]">عنوان الفيديو</label>
          <input
            type="text"
            value={title}
            onChange={(e) => setTitle(e.target.value)}
            placeholder="مثال: الدرس الأول - مراجعة"
            className="w-full rounded-xl border border-[var(--admin-border)] bg-[var(--admin-card)] px-4 py-3 text-sm text-[var(--admin-text)] placeholder-[var(--admin-border)] outline-none focus:border-[var(--admin-primary)] focus:ring-1 focus:ring-[var(--admin-primary)] transition-all"
            required
          />
        </div>
        <div className="w-40 space-y-2">
          <Dropdown
            label="المنصة"
            value={provider}
            onChange={(v) => setProvider(v as string)}
            size="sm"
            options={[
              { value: 'YouTube', label: 'YouTube' },
              { value: 'vk', label: 'VK (فيكونتاكتي)' },
            ]}
          />
        </div>
        <div className="flex-1 space-y-2 min-w-[200px]">
          <label className="text-xs font-bold text-[var(--admin-muted)]">رابط الفيديو (أو المعرف)</label>
          <input
            type="text"
            value={urlOrEmbedCode}
            onChange={(e) => {
              const val = e.target.value;
              if (val.includes('vk.com/video') || val.includes('vk.com/video_ext')) {
                setProvider('vk');
              } else if (val.includes('youtube.com') || val.includes('youtu.be')) {
                setProvider('YouTube');
              }
              setUrlOrEmbedCode(val);
            }}
            placeholder={provider === 'vk' ? 'مثال: oid=-22822305&id=456241864' : 'رابط الفيديو'}
            className="w-full rounded-xl border border-[var(--admin-border)] bg-[var(--admin-card)] px-4 py-3 text-sm text-[var(--admin-text)] placeholder-[var(--admin-border)] outline-none focus:border-[var(--admin-primary)] focus:ring-1 focus:ring-[var(--admin-primary)] transition-all"
            required
          />
        </div>
      </div>
      <div className="flex flex-wrap items-end gap-4">
        <div className="w-32">
          <NumberField value={order} onChange={setOrder} minValue={1}>
            <NumberField.Label className="text-xs font-bold text-[var(--admin-muted)] text-right block w-full mb-2">ترتيب العرض</NumberField.Label>
            <NumberField.Group className="h-[46px] w-full bg-[var(--admin-card)] hover:shadow-none">
              <NumberField.DecrementButton />
              <NumberField.Input className="bg-[var(--admin-card)]" />
              <NumberField.IncrementButton />
            </NumberField.Group>
          </NumberField>
        </div>
        <div className="w-40">
          <NumberField value={limit} onChange={setLimit} minValue={1}>
            <NumberField.Label className="text-xs font-bold text-[var(--admin-muted)] text-right block w-full mb-2">الحد الأقصى للمشاهدات</NumberField.Label>
            <NumberField.Group className="h-[46px] w-full bg-[var(--admin-card)] hover:shadow-none">
              <NumberField.DecrementButton />
              <NumberField.Input className="bg-[var(--admin-card)]" />
              <NumberField.IncrementButton />
            </NumberField.Group>
          </NumberField>
        </div>
        <div className="flex items-center gap-2 h-[46px] px-2">
          <input
            id="new-video-is-active"
            type="checkbox"
            checked={isActive}
            onChange={(e) => setIsActive(e.target.checked)}
            className="h-4 w-4 rounded border-[var(--admin-border)] text-[var(--admin-primary)] focus:ring-[var(--admin-primary)] cursor-pointer"
          />
          <label htmlFor="new-video-is-active" className="text-sm font-bold text-[var(--admin-text)] cursor-pointer select-none">
            تفعيل الفيديو مباشرة للطلاب
          </label>
        </div>
        <NeumorphButton
          type="submit"
          disabled={saving || !title.trim() || !urlOrEmbedCode.trim()}
          loading={saving}
          intent="primary"
          size="lg"
          pill
          className="whitespace-nowrap ml-auto"
        >
          إضافة الفيديو
        </NeumorphButton>
      </div>
    </form>
  );
}
