'use client';

import { useState } from 'react';
import { adminService } from '@/services/admin-service';
import { Checkbox, Label as CheckboxLabel } from '@/components/ui/checkbox';
import { NumberField } from '@/components/ui/number-field';
import toast from 'react-hot-toast';
import NeumorphButton from '@/components/ui/neumorph-button';

interface PackageDetailsFormProps {
  pkg: {
    id: string;
    name: string;
    description: string;
    price: number;
    isActive: boolean;
    programId?: string;
  };
  onSuccess?: () => void;
}

export function PackageDetailsForm({ pkg, onSuccess }: PackageDetailsFormProps) {
  const [name, setName] = useState(pkg.name || '');
  const [description, setDescription] = useState(pkg.description || '');
  const [price, setPrice] = useState(pkg.price || 0);
  const [isActive, setIsActive] = useState(pkg.isActive !== false);
  const [saving, setSaving] = useState(false);

  async function handleSubmit(e: React.FormEvent) {
    e.preventDefault();
    if (!name.trim()) return;

    try {
      setSaving(true);
      await adminService.updatePackage(pkg.id, {
        name,
        description,
        price,
        isActive,
      });
      toast.success('تم تحديث بيانات الباقة بنجاح.');
      onSuccess?.();
    } catch (error) {
      toast.error('حدث خطأ أثناء تحديث الباقة، يرجى المحاولة مرة أخرى.');
    } finally {
      setSaving(false);
    }
  }

  return (
    <form onSubmit={handleSubmit} className="space-y-6">
      <div className="grid grid-cols-1 gap-6 md:grid-cols-2">
        <div className="space-y-2">
          <label className="text-sm font-bold text-[var(--admin-text)]">اسم الباقة</label>
          <input
            type="text"
            value={name}
            onChange={(e) => setName(e.target.value)}
            placeholder="مثال: الباقة التأسيسية"
            required
            className="admin-input"
          />
        </div>
        <div className="space-y-2">
          <NumberField
            minValue={0}
            step={0.1}
            value={price}
            onChange={setPrice}
          >
            <NumberField.Label className="text-sm font-bold text-[var(--admin-text)] mb-2 block">السعر الإجمالي (جنيه مصري)</NumberField.Label>
            <NumberField.Group className="h-[46px]">
              <NumberField.DecrementButton />
              <NumberField.Input />
              <NumberField.IncrementButton />
            </NumberField.Group>
          </NumberField>
        </div>
      </div>

      <div className="space-y-2">
        <label className="text-sm font-bold text-[var(--admin-text)]">الوصف المفصل</label>
        <textarea
          rows={4}
          value={description}
          onChange={(e) => setDescription(e.target.value)}
          placeholder="اكتب وصفاً مفصلاً يوضح محتوى ومميزات هذه الباقة للطلاب..."
          className="admin-input"
        />
      </div>

      <div className="flex items-center rounded-xl border border-[var(--admin-border)] bg-[var(--admin-bg)] p-4 transition-all hover:bg-[var(--admin-card-soft)] hover:shadow-sm">
        <Checkbox id="isActive" isSelected={isActive} onChange={setIsActive}>
          <Checkbox.Control>
            <Checkbox.Indicator />
          </Checkbox.Control>
          <Checkbox.Content>
            <CheckboxLabel className="cursor-pointer">تفعيل الباقة (تظهر للطلاب للتسجيل)</CheckboxLabel>
          </Checkbox.Content>
        </Checkbox>
      </div>

      <div className="flex justify-end border-t border-[var(--admin-border)] pt-6">
        <NeumorphButton
          type="submit"
          disabled={saving}
          loading={saving}
          intent="primary"
          size="lg"
          pill
          className="px-8"
        >
          حفظ التغييرات
        </NeumorphButton>
      </div>
    </form>
  );
}
