import { Activity, Bot, CheckCircle, MessageSquare, UserCheck } from 'lucide-react';
import type { AIPolicy, AIStats } from '@/services/live-support-ai-service';
import { LiveSupportEmptyState } from '../shared/LiveSupportEmptyState';
import { LiveSupportSkeleton } from '../shared/LiveSupportSkeleton';

export function AIOverview({ policy, stats, loading }: { policy?: AIPolicy; stats?: AIStats; loading: boolean }) {
  if (loading) return <LiveSupportSkeleton rows={3} />;
  if (!stats) return <LiveSupportEmptyState title="لا توجد إحصائيات" description="اختر فترة أخرى أو حاول لاحقًا." />;
  const metrics = [
    ['المحادثات النشطة', stats.activeConversations, Bot],
    ['المشكلات المحلولة', stats.resolvedIssues, CheckCircle],
    ['التحويلات البشرية', stats.handoffs, UserCheck],
    ['ردود المساعد', stats.totalMessagesSent, MessageSquare],
    ['الإجراءات الناجحة', stats.successfulActions, Activity],
  ] as const;
  return <section aria-labelledby="ai-overview-title" className="space-y-4">
    <div><h2 id="ai-overview-title" className="font-bold text-slate-950">نظرة عامة</h2><p className="text-sm text-slate-600">السياسة المنشورة: {policy ? `الإصدار ${policy.versionNumber}` : 'لا توجد'}.</p></div>
    <div className="grid gap-3 sm:grid-cols-2 xl:grid-cols-5">{metrics.map(([label, value, Icon]) => <div key={label} className="rounded-2xl border border-slate-200 bg-white p-4"><Icon className="size-5 text-cyan-700"/><p className="mt-3 text-xs text-slate-500">{label}</p><p className="text-2xl font-bold text-slate-950">{value.toLocaleString('ar-EG')}</p></div>)}</div>
  </section>;
}
