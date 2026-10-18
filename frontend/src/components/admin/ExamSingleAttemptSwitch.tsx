'use client';

export function ExamSingleAttemptSwitch({ enabled, onChange, disabled = false }: {
  enabled: boolean; onChange: (enabled: boolean) => void; disabled?: boolean;
}) {
  return <label className="flex min-h-11 items-start gap-3 text-sm text-[var(--admin-text)]">
    <input type="checkbox" role="switch" checked={enabled} disabled={disabled}
      onChange={event => onChange(event.target.checked)} className="mt-1 h-5 w-5 shrink-0 accent-[var(--admin-primary)]" />
    <span className="space-y-1">
      <span className="block font-bold">محاولة واحدة فقط</span>
      <span className="block leading-6 text-[var(--admin-muted)]">بعد التسليم أو انتهاء الوقت، لا يمكن إعادة الامتحان سواء نجح الطالب أو سقط. يمكن استكمال المحاولة المفتوحة بنفس الوقت. الإدارة أو الدعم صاحب الصلاحية يقدر يحذف المحاولة من ملف الطالب للسماح بمحاولة جديدة.</span>
    </span>
  </label>;
}
