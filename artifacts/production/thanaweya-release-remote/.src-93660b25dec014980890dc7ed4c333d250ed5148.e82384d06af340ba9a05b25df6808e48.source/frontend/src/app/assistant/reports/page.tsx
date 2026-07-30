'use client';

import { RefreshCw } from 'lucide-react';
import { AssistantShellChrome } from '@/components/assistant/AssistantShellChrome';
import { AdvancedReportsCenter } from '@/components/reports/AdvancedReportsCenter';

export default function AssistantReportsPage() {
  return <AssistantShellChrome activePath="/assistant/reports" sectionLabel="العمليات" pageTitle="مركز التقارير" subtitle="ابنِ التقارير من بيانات المنصة، راجع النتائج وصدّرها."><div className="mb-4 inline-flex items-center gap-1.5 rounded-full bg-[var(--admin-primary-15)] px-3 py-1.5 text-xs font-black text-[var(--admin-primary)]"><RefreshCw className="h-3.5 w-3.5" />توقيت القاهرة</div><AdvancedReportsCenter audience="admin" /></AssistantShellChrome>;
}
