'use client';

import { useState } from 'react';
import { AdvancedReportsCenter } from './AdvancedReportsCenter';
import { LessonStudentReports } from './LessonStudentReports';
import type { ReportAudience } from '@/services/advanced-report-service';

export function ReportsWorkspace({ audience }: { audience: ReportAudience }) {
  const [section, setSection] = useState<'learning' | 'advanced'>('learning');
  const [advancedOpened, setAdvancedOpened] = useState(false);
  return <div className="space-y-6" dir="rtl">
    <nav aria-label="أقسام مركز التقارير" className="flex flex-wrap gap-2 border-b border-[var(--admin-border)] pb-3">
      {([{ key: 'learning', label: 'الحصص والطلاب' }, { key: 'advanced', label: 'التقارير المتقدمة' }] as const).map(tab =>
        <button key={tab.key} type="button" aria-pressed={section === tab.key} aria-controls={`reports-${tab.key}`}
          onClick={() => { setSection(tab.key); if (tab.key === 'advanced') setAdvancedOpened(true); }}
          className={`min-h-11 rounded-xl px-5 text-sm font-bold focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-[var(--admin-primary)] ${section === tab.key ? 'bg-[var(--admin-primary)] text-white' : 'text-[var(--admin-text)] hover:bg-[var(--admin-hover)]'}`}>{tab.label}</button>)}
    </nav>
    <section id="reports-learning" aria-label="الحصص والطلاب" hidden={section !== 'learning'}><LessonStudentReports audience={audience} /></section>
    {advancedOpened && <section id="reports-advanced" aria-label="التقارير المتقدمة" hidden={section !== 'advanced'}><AdvancedReportsCenter audience={audience} /></section>}
  </div>;
}
