'use client';

import { useState } from 'react';
import { adminService } from '@/services/admin-service';
import toast from 'react-hot-toast';
import { NumberField } from '@/components/ui/number-field';
import NeumorphButton from '@/components/ui/neumorph-button';

interface AddLessonFormProps {
  sectionId: string;
  onSuccess?: () => void;
}

export function AddLessonForm({ sectionId, onSuccess }: AddLessonFormProps) {
  const [title, setTitle] = useState('');
  const [summary, setSummary] = useState('');
  const [order, setOrder] = useState(1);
  const [price, setPrice] = useState(0);
  const [saving, setSaving] = useState(false);

  async function handleSubmit(e: React.FormEvent) {
    e.preventDefault();
    if (!title.trim() || !summary.trim()) return;

    try {
      setSaving(true);
      await adminService.createLesson({ sectionId, title, summary, order, price });
      toast.success('تمت إضافة الحصة بنجاح.');
      setTitle('');
      setSummary('');
      setPrice(0);
      setOrder((prev) => prev + 1);
      onSuccess?.();
    } catch (error) {
      toast.error('حدث خطأ أثناء إضافة الحصة، أعد المحاولة.');
    } finally {
      setSaving(false);
    }
  }

  return (
    <form id="add-lesson-form" onSubmit={handleSubmit} className="flex flex-col gap-4 rounded-xl border border-[var(--admin-border)] bg-[var(--admin-card-strong)] p-5">
      <div className="grid grid-cols-1 md:grid-cols-4 gap-4">
        <div className="md:col-span-3 space-y-2">
          <label className="text-xs font-bold text-[var(--admin-muted)]">عنوان الحصة</label>
          <input
            type="text"
            value={title}
            onChange={(e) => setTitle(e.target.value)}
            placeholder="مثال: مقدمة في البلاغة..."
            required
            className="admin-input"
          />
        </div>
        <NumberField value={order} onChange={setOrder} minValue={1}>
          <NumberField.Label className="text-xs font-bold text-[var(--admin-muted)] text-right block w-full mb-2">ترتيب العرض</NumberField.Label>
          <NumberField.Group className="h-12 w-full">
            <NumberField.DecrementButton />
            <NumberField.Input />
            <NumberField.IncrementButton />
          </NumberField.Group>
        </NumberField>

        <NumberField value={price} onChange={setPrice} minValue={0}>
          <NumberField.Label className="text-xs font-bold text-[var(--admin-muted)] text-right block w-full mb-2">السعر (جنيه مصري)</NumberField.Label>
          <NumberField.Group className="h-12 w-full">
            <NumberField.DecrementButton />
            <NumberField.Input />
            <NumberField.IncrementButton />
          </NumberField.Group>
        </NumberField>
      </div>
      
      <div className="space-y-2">
        <label className="text-xs font-bold text-[var(--admin-muted)]">وصف قصير للحصة</label>
        <textarea
          value={summary}
          onChange={(e) => setSummary(e.target.value)}
          placeholder="اكتب نبذة مختصرة عما سيتعلمه الطالب في هذه الحصة..."
          required
          rows={2}
          className="admin-input resize-none"
        />
      </div>

      <div className="flex justify-end pt-2">
        <NeumorphButton
          type="submit"
          disabled={saving || !title.trim() || !summary.trim()}
          loading={saving}
          intent="primary"
          size="lg"
          pill
          className="whitespace-nowrap"
        >
          إضافة الحصة
        </NeumorphButton>
      </div>
    </form>
  );
}
