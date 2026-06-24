import { Bot } from 'lucide-react';

export function AIHandoffSummary({ summary, reasonCode, policyVersion }: { summary?: string | null; reasonCode?: string | null; policyVersion?: number | null }) {
  if (!summary && !reasonCode) return null;
  return <section className="border-b border-cyan-100 bg-cyan-50 p-4 text-sm text-slate-700"><div className="flex items-center gap-2 font-bold text-slate-900"><Bot size={17}/>ملخص المساعد قبل التحويل</div><p className="mt-2 leading-6">{summary || 'لا يوجد ملخص إضافي.'}</p><p className="mt-1 text-xs text-slate-500">السبب: {reasonCode || 'غير محدد'}{policyVersion ? ` · سياسة ${policyVersion}` : ''}</p></section>;
}
