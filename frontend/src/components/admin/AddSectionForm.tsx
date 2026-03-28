'use client';

import { useState } from 'react';
import { adminService } from '@/services/admin-service';
import toast from 'react-hot-toast';
import { NumberField } from '@/components/ui/number-field';
import NeumorphButton from '@/components/ui/neumorph-button';

interface AddSectionFormProps {
  termId: string;
  onSuccess?: () => void;
}

export function AddSectionForm({ termId, onSuccess }: AddSectionFormProps) {
  const [title, setTitle] = useState('');
  const [order, setOrder] = useState(1);
  const [price, setPrice] = useState(0);
  const [saving, setSaving] = useState(false);

  async function handleSubmit(e: React.FormEvent) {
    e.preventDefault();
    if (!title.trim()) return;

    try {
      setSaving(true);
      await adminService.createSection({ termId, title, order, price });
      toast.success('تمت إضافة القسم بنجاح.');
      setTitle('');
      setPrice(0);
      setOrder((prev) => prev + 1);
      onSuccess?.();
    } catch (error) {
      toast.error('حدث خطأ أثناء إضافة القسم، أعد المحاولة.');
    } finally {
      setSaving(false);
    }
  }

  return (
    <form id="add-section-form" onSubmit={handleSubmit} className="flex items-end gap-3 rounded-xl border border-[var(--admin-border)] bg-[var(--admin-card-strong)] p-5">
      <div className="flex-grow space-y-2">
        <label className="text-xs font-bold text-[var(--admin-muted)]">عنوان القسم</label>
        <input
          type="text"
          value={title}
          onChange={(e) => setTitle(e.target.value)}
          placeholder="مثال: الوحدة الأولى: التفاضل..."
          required
          className="admin-input"
        />
      </div>
      <div className="w-32">
        <NumberField value={order} onChange={setOrder} minValue={1}>
          <NumberField.Label className="text-xs font-bold text-[var(--admin-muted)] text-right block w-full mb-2">ترتيب العرض</NumberField.Label>
          <NumberField.Group className="h-12 w-full">
            <NumberField.DecrementButton />
            <NumberField.Input />
            <NumberField.IncrementButton />
          </NumberField.Group>
        </NumberField>
      </div>
      <div className="w-40">
        <NumberField value={price} onChange={setPrice} minValue={0}>
          <NumberField.Label className="text-xs font-bold text-[var(--admin-muted)] text-right block w-full mb-2">السعر (جنيه مصري)</NumberField.Label>
          <NumberField.Group className="h-12 w-full">
            <NumberField.DecrementButton />
            <NumberField.Input />
            <NumberField.IncrementButton />
          </NumberField.Group>
        </NumberField>
      </div>
      <NeumorphButton
        type="submit"
        disabled={saving || !title.trim()}
        loading={saving}
        intent="primary"
        size="lg"
        pill
        className="whitespace-nowrap"
      >
        إضافة القسم
      </NeumorphButton>
    </form>
  );
}
