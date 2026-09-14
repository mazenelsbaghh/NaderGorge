'use client';

import { useEffect, useState, type FormEvent } from 'react';
import { Check } from 'lucide-react';
import toast from 'react-hot-toast';
import { NumberField } from '@/components/ui/number-field';
import NeumorphButton from '@/components/ui/neumorph-button';

interface ContentBasicDetailsFormProps {
  title: string;
  order: number;
  price: number;
  summary?: string | null;
  summaryLabel?: string;
  onSave: (payload: { title: string; order: number; price: number; summary?: string }) => Promise<void>;
}

export function ContentBasicDetailsForm({
  title,
  order,
  price,
  summary,
  summaryLabel = 'الوصف',
  onSave,
}: ContentBasicDetailsFormProps) {
  const [formTitle, setFormTitle] = useState(title);
  const [formOrder, setFormOrder] = useState(order);
  const [formPrice, setFormPrice] = useState(price);
  const [formSummary, setFormSummary] = useState(summary ?? '');
  const [saving, setSaving] = useState(false);

  useEffect(() => {
    setFormTitle(title);
    setFormOrder(order);
    setFormPrice(price);
    setFormSummary(summary ?? '');
  }, [order, price, summary, title]);

  async function handleSubmit(e: FormEvent) {
    e.preventDefault();
    if (!formTitle.trim()) {
      toast.error('اكتب الاسم أولاً.');
      return;
    }

    try {
      setSaving(true);
      await onSave({
        title: formTitle.trim(),
        order: formOrder,
        price: formPrice,
        summary: formSummary.trim() || undefined,
      });
      toast.success('تم حفظ التعديلات.');
    } catch {
      toast.error('تعذر حفظ التعديلات.');
    } finally {
      setSaving(false);
    }
  }

  return (
    <form onSubmit={handleSubmit} className="rounded-3xl border border-[var(--admin-border)] bg-[var(--admin-card)] p-6 shadow-sm">
      <div className="mb-5 flex items-center justify-between gap-3">
        <h3 className="text-lg font-black text-[var(--admin-text)]">الإعدادات الأساسية</h3>
        <NeumorphButton type="submit" disabled={saving || !formTitle.trim()} loading={saving} intent="primary" size="sm" pill>
          <Check className="h-3.5 w-3.5" />
          حفظ
        </NeumorphButton>
      </div>

      <div className="grid gap-4 md:grid-cols-[minmax(0,1fr)_140px_160px]">
        <label className="space-y-2">
          <span className="text-sm font-bold text-[var(--admin-text)]">الاسم</span>
          <input
            value={formTitle}
            onChange={(event) => setFormTitle(event.target.value)}
            className="admin-input"
          />
        </label>

        <NumberField value={formOrder} onChange={setFormOrder} minValue={1}>
          <NumberField.Label className="mb-2 block text-sm font-bold text-[var(--admin-text)]">الترتيب</NumberField.Label>
          <NumberField.Group className="h-[46px]">
            <NumberField.DecrementButton />
            <NumberField.Input />
            <NumberField.IncrementButton />
          </NumberField.Group>
        </NumberField>

        <NumberField value={formPrice} onChange={setFormPrice} minValue={0}>
          <NumberField.Label className="mb-2 block text-sm font-bold text-[var(--admin-text)]">السعر (جنيه)</NumberField.Label>
          <NumberField.Group className="h-[46px]">
            <NumberField.DecrementButton />
            <NumberField.Input />
            <NumberField.IncrementButton />
          </NumberField.Group>
        </NumberField>
      </div>

      {summary !== undefined && (
        <label className="mt-4 block space-y-2">
          <span className="text-sm font-bold text-[var(--admin-text)]">{summaryLabel}</span>
          <textarea
            value={formSummary}
            onChange={(event) => setFormSummary(event.target.value)}
            rows={3}
            className="admin-input resize-none"
          />
        </label>
      )}
    </form>
  );
}
