'use client';

import { ShieldCheck } from 'lucide-react';
import { TeacherPage } from '@/components/teacher/TeacherShellChrome';
import { ReportsWorkspace } from '@/components/reports/ReportsWorkspace';

export default function TeacherReportsPageClient() {
  return (
    <TeacherPage
      activePath="/teacher/reports"
      sectionLabel="تحليل الأداء"
      pageTitle="مركز التقارير"
      subtitle="راجع حضور حصصك ومشاهدة كل طالب، أو افتح التقارير المتقدمة داخل نطاقك."
      headerAccessory={<span className="inline-flex items-center gap-1.5 rounded-full bg-[var(--admin-primary-15)] px-3 py-1.5 text-xs font-black text-[var(--admin-primary)]"><ShieldCheck className="h-3.5 w-3.5" /> نطاق المدرس محمي</span>}
    >
      <ReportsWorkspace audience="teacher" />
    </TeacherPage>
  );
}
