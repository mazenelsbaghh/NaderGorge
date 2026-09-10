'use client';

import { ArrowDown, ArrowUp } from 'lucide-react';

const labels: Record<string, string> = {
  FatherSecondary: 'رقم ولي الأمر الإضافي',
  FatherPrimary: 'رقم الأب',
  Mother: 'رقم الأم',
};
export const defaultParentWhatsAppPriority = 'FatherSecondary,FatherPrimary,Mother';

export function ParentWhatsAppPrioritySettings({ value, onChange, disabled }: {
  value: string; onChange: (priority: string) => void; disabled: boolean;
}) {
  const roles = value.split(',');
  function move(index: number, offset: number) {
    const reordered = [...roles];
    [reordered[index], reordered[index + offset]] = [reordered[index + offset], reordered[index]];
    onChange(reordered.join(','));
  }
  return <fieldset disabled={disabled} className="space-y-4 rounded-xl border border-[var(--admin-border)] bg-[var(--admin-card)] p-5">
    <legend className="px-2 text-lg font-bold text-[var(--admin-text)]">أولوية أرقام ولي الأمر</legend>
    <p className="text-sm leading-6 text-[var(--admin-muted)]">تُرسل رسالة النتيجة إلى أول رقم صالح حسب هذا الترتيب. إذا كان الرقم غير مسجل أو غير صالح نستخدم التالي.</p>
    <ol className="divide-y divide-[var(--admin-border)]">
      {roles.map((role, index) => <li key={role} className="flex items-center justify-between gap-3 py-3">
        <span className="font-bold text-[var(--admin-text)]">{index + 1}. {labels[role]}</span>
        <div className="flex gap-2">
          <button type="button" aria-label={`تقديم ${labels[role]}`} disabled={disabled || index === 0} onClick={() => move(index, -1)} className="admin-btn-ghost grid size-11 place-items-center disabled:opacity-40"><ArrowUp aria-hidden size={18} /></button>
          <button type="button" aria-label={`تأخير ${labels[role]}`} disabled={disabled || index === roles.length - 1} onClick={() => move(index, 1)} className="admin-btn-ghost grid size-11 place-items-center disabled:opacity-40"><ArrowDown aria-hidden size={18} /></button>
        </div>
      </li>)}
    </ol>
  </fieldset>;
}
