'use client';

import { ShieldCheck } from 'lucide-react';
import { TeacherPage } from '@/components/teacher/TeacherShellChrome';
import { AdvancedReportsCenter } from '@/components/reports/AdvancedReportsCenter';

export default function TeacherReportsPageClient() {
  return (
    <TeacherPage
      activePath="/teacher/reports"
      sectionLabel="تحليل الأداء"
      pageTitle="مركز التقارير"
      subtitle="حلّل الطلاب والشراء والمشاهدة والاختبارات والمالية داخل نطاقك فقط، مع جدول وتصدير كامل."
      headerAccessory={<span className="inline-flex items-center gap-1.5 rounded-full bg-[var(--admin-primary-15)] px-3 py-1.5 text-xs font-black text-[var(--admin-primary)]"><ShieldCheck className="h-3.5 w-3.5" /> نطاق المدرس محمي</span>}
    >
      <AdvancedReportsCenter audience="teacher" />
    </TeacherPage>
  );
}
