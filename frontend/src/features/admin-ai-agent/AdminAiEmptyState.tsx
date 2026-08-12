import { Bot, LockKeyhole, ShieldCheck } from 'lucide-react';
export function AdminAiEmptyState() {
  return (
    <div className="m-auto max-w-xl p-6 text-center">
      <Bot className="mx-auto h-12 w-12 text-[var(--admin-primary)]" />
      <h2 className="mt-4 text-xl font-black">اسأل عن المنصة من مكان واحد</h2>
      <p className="mt-2 text-sm leading-7 text-[var(--admin-muted)]">
        إجابات موثقة من البيانات المسموح بها، وأي تعديل يظهر أولًا كمقترح مستقل.
      </p>
      <div className="mt-5 grid gap-3 text-right sm:grid-cols-2">
        <p className="flex gap-2 text-sm">
          <ShieldCheck className="h-5 w-5 text-[var(--admin-primary)]" />
          لا يوجد SQL حر أو تنفيذ تلقائي.
        </p>
        <p className="flex gap-2 text-sm">
          <LockKeyhole className="h-5 w-5 text-[var(--admin-primary)]" />
          الأسرار والجلسات محظورة دائمًا.
        </p>
      </div>
    </div>
  );
}
