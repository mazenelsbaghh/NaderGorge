import { Bot } from 'lucide-react';

export function AIHandoffSummary({ summary, reasonCode, policyVersion }: { summary?: string | null; reasonCode?: string | null; policyVersion?: number | null }) {
  if (!summary && !reasonCode) return null;
  return <section aria-label="ملخص المساعد قبل التحويل" className="border-b border-[var(--admin-border)] bg-[var(--admin-primary-15)] p-4 text-sm text-[var(--admin-text)]"><div className="flex items-center gap-2 font-bold text-[var(--admin-primary)]"><Bot size={17}/>ملخص المساعد قبل التحويل</div><p className="mt-2 leading-6">{summary || 'لا يوجد ملخص إضافي.'}</p><p className="mt-1 text-xs text-[var(--admin-muted)]">السبب: {reasonCode || 'غير محدد'}{policyVersion ? ` · سياسة ${policyVersion}` : ''}</p></section>;
}
