'use client';

import { useState } from 'react';
import { adminService } from '@/services/admin-service';
import { Checkbox, Label } from '@/components/ui/checkbox';
import { NumberField } from '@/components/ui/number-field';
import toast from 'react-hot-toast';
import NeumorphButton from '@/components/ui/neumorph-button';

interface AddHomeworkFormProps {
  lessonId: string;
  onSuccess?: () => void;
}

export function AddHomeworkForm({ lessonId, onSuccess }: AddHomeworkFormProps) {
  const [title, setTitle] = useState('');
  const [instructions, setInstructions] = useState('');
  const [isMandatory, setIsMandatory] = useState(true);
  const [totalScore, setTotalScore] = useState(100);
  const [requiredPointsToPass, setRequiredPointsToPass] = useState(50);
  const [saving, setSaving] = useState(false);

  async function handleSubmit(e: React.FormEvent) {
    e.preventDefault();
    if (!title.trim()) return;

    if (totalScore <= 0) {
      toast.error('الدرجة النهائية يجب أن تكون أكبر من صفر');
      return;
    }

    if (requiredPointsToPass > totalScore) {
      toast.error('درجة النجاح لا يمكن أن تكون أكبر من الدرجة النهائية');
      return;
    }

    try {
      setSaving(true);
      await adminService.attachHomework(lessonId, {
        title,
        instructions,
        isMandatory,
        totalScore,
        requiredPointsToPass,
        questions: [], // Currently defaults to empty, admin can edit later
      });
      toast.success('تم إنشاء الواجب بنجاح.');
      setTitle('');
      setInstructions('');
      onSuccess?.();
    } catch (error) {
      toast.error('حدث خطأ أثناء إضافة الواجب، أعد المحاولة.');
    } finally {
      setSaving(false);
    }
  }

  return (
    <form id="add-homework-form" onSubmit={handleSubmit} className="flex flex-col gap-4">
      <div className="grid grid-cols-1 gap-4 md:grid-cols-2">
        <div className="space-y-2">
          <label className="text-xs font-bold text-[var(--admin-muted)]">عنوان الواجب</label>
          <input
            type="text"
            value={title}
            onChange={(e) => setTitle(e.target.value)}
            placeholder="مثال: الواجب الأسبوعي للدرس الأول"
            className="w-full rounded-xl border border-[var(--admin-border)] bg-[var(--admin-card)] px-4 py-3 text-sm text-[var(--admin-text)] placeholder-[var(--admin-border)] outline-none focus:border-[var(--admin-primary)] focus:ring-1 focus:ring-[var(--admin-primary)] transition-all"
            required
          />
        </div>
        <div className="space-y-2">
          <label className="text-xs font-bold text-[var(--admin-muted)]">إرشادات إضافية</label>
          <input
            type="text"
            value={instructions}
            onChange={(e) => setInstructions(e.target.value)}
            placeholder="مثال: يرجى حل الواجب قبل يوم الخميس..."
            className="w-full rounded-xl border border-[var(--admin-border)] bg-[var(--admin-card)] px-4 py-3 text-sm text-[var(--admin-text)] placeholder-[var(--admin-border)] outline-none focus:border-[var(--admin-primary)] focus:ring-1 focus:ring-[var(--admin-primary)] transition-all"
          />
        </div>
        <div className="space-y-2">
          <NumberField
             minValue={1}
             value={totalScore}
             onChange={setTotalScore}
          >
             <NumberField.Label className="text-xs font-bold text-[var(--admin-muted)] mb-2 block">الدرجة النهائية</NumberField.Label>
             <NumberField.Group className="h-[46px]">
               <NumberField.DecrementButton />
               <NumberField.Input />
               <NumberField.IncrementButton />
             </NumberField.Group>
          </NumberField>

          <NumberField
             minValue={0}
             maxValue={totalScore}
             value={requiredPointsToPass}
             onChange={setRequiredPointsToPass}
          >
             <NumberField.Label className="text-xs font-bold text-[var(--admin-muted)] mb-2 block">الحد الأدنى للنجاح (نقطة)</NumberField.Label>
             <NumberField.Group className="h-[46px]">
               <NumberField.DecrementButton />
               <NumberField.Input />
               <NumberField.IncrementButton />
             </NumberField.Group>
          </NumberField>
        </div>
        <div className="flex items-center pt-8">
          <Checkbox isSelected={isMandatory} onChange={setIsMandatory}>
            <Checkbox.Control>
              <Checkbox.Indicator />
            </Checkbox.Control>
            <Checkbox.Content>
              <Label>الواجب إلزامي</Label>
            </Checkbox.Content>
          </Checkbox>
        </div>
      </div>
      <div className="mt-4 flex justify-end">
        <NeumorphButton
          type="submit"
          disabled={saving || !title.trim()}
          loading={saving}
          intent="primary"
          size="lg"
          pill
          className="px-8"
        >
          إنشاء الواجب
        </NeumorphButton>
      </div>
    </form>
  );
}
