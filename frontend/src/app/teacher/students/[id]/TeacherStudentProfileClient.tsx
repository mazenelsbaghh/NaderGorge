'use client';

import { useEffect, useState } from 'react';
import Link from 'next/link';
import { AdminTabBar, type AdminTab } from '@/components/admin';
import { TeacherPage } from '@/components/teacher/TeacherShellChrome';
import { StudentAssessmentHistory } from '@/components/admin/StudentAssessmentHistory';
import { LessonStudentReports } from '@/components/reports/LessonStudentReports';
import { getTeacherStudentProfile, type TeacherStudentProfile } from '@/services/teacher-student-profile-service';
import { getApiErrorSummary } from '@/lib/api-errors';
import { TeacherStudentOverview, TeacherStudentSubscriptions, TeacherStudentFinances, TeacherStudentNotes, TeacherStudentOverrides, TeacherStudentAudit } from '@/components/teacher/TeacherStudentProfileSections';
import { TeacherStudentWatchHistory } from '@/components/teacher/TeacherStudentWatchHistory';

type ProfileTab = 'overview' | 'watching' | 'assessments' | 'subscriptions' | 'financials' | 'overrides' | 'notes' | 'audit' | 'report';
const tabs: AdminTab<ProfileTab>[] = [
  { key: 'overview', label: 'نظرة عامة' }, { key: 'subscriptions', label: 'الاشتراكات' },
  { key: 'watching', label: 'المشاهدة والجلسات' }, { key: 'assessments', label: 'الامتحانات والواجبات' },
  { key: 'report', label: 'تقرير المشاهدة' }, { key: 'financials', label: 'الماليات' },
  { key: 'overrides', label: 'تجاوزات المشاهدة' }, { key: 'notes', label: 'الملاحظات' }, { key: 'audit', label: 'سجل النشاط' },
];
export default function TeacherStudentProfileClient({ studentId }: { studentId: string }) {
  const [profile, setProfile] = useState<TeacherStudentProfile | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState('');
  const [version, setVersion] = useState(0);
  const [activeTab, setActiveTab] = useState<ProfileTab>('overview');
  useEffect(() => {
    const controller = new AbortController();
    setLoading(true); setError(''); setProfile(null);
    getTeacherStudentProfile(studentId, controller.signal)
      .then(student => { if (!controller.signal.aborted) setProfile(student); })
      .catch(reason => { if (!controller.signal.aborted) setError(getApiErrorSummary(reason, 'تعذر تحميل ملف الطالب. أعد المحاولة.')); })
      .finally(() => { if (!controller.signal.aborted) setLoading(false); });
    return () => controller.abort();
  }, [studentId, version]);
  const student = profile?.profile;
  const availableTabs = tabs.filter(tab => (tab.key !== 'financials' || profile?.canViewFinance) && (tab.key !== 'report' || profile?.canViewReports));
  return <TeacherPage activePath="/teacher/students" sectionLabel="الطلاب" pageTitle="ملف الطالب الشامل" subtitle="بيانات الطالب وتفاصيل دراسته واشتراكاته المرتبطة بك."
    action={<Link href="/teacher/students" className="inline-flex min-h-11 items-center rounded-xl border border-[var(--admin-border)] px-4 text-sm font-bold text-[var(--admin-text)]">العودة إلى الطلاب</Link>}>
    <div dir="rtl" className="space-y-6">
      {loading && <p role="status" className="py-12 text-center text-[var(--admin-muted)]">جاري تحميل ملف الطالب…</p>}
      {error && <div role="alert" className="rounded-xl border border-[var(--admin-danger)] p-5 text-[var(--admin-danger)]">{error}<button type="button" onClick={() => setVersion(current => current + 1)} className="mr-3 min-h-11 underline">إعادة تحميل الملف</button></div>}
      {student && profile && <>
        <header className="flex flex-wrap items-center justify-between gap-3 border-b border-[var(--admin-border)] pb-5"><div><h2 className="text-2xl font-bold text-[var(--admin-text)]">{student.fullName}</h2><p className="mt-2 text-sm text-[var(--admin-muted)]"><bdi>{student.phone}</bdi> · {student.studentCode || 'الكود غير مسجل'}</p></div><p className="text-sm font-bold text-[var(--admin-text)]">{student.isActive ? 'حساب فعّال' : 'حساب موقوف'}</p></header>
        <AdminTabBar tabs={availableTabs} activeTab={activeTab} onSelect={setActiveTab} />
        <div aria-live="polite">
          {activeTab === 'overview' && <TeacherStudentOverview student={student} />}
          {activeTab === 'subscriptions' && <TeacherStudentSubscriptions student={student} showFinance={profile.canViewFinance} />}
          {activeTab === 'watching' && <TeacherStudentWatchHistory tracking={student.watchTracking} />}
          {activeTab === 'assessments' && <StudentAssessmentHistory examHistory={student.examHistory} homeworkHistory={student.homeworkHistory} studentName={student.fullName} readOnly />}
          {activeTab === 'financials' && profile.canViewFinance && <TeacherStudentFinances student={student} />}
          {activeTab === 'overrides' && <TeacherStudentOverrides student={student} />}
          {activeTab === 'notes' && <TeacherStudentNotes student={student} />}
          {activeTab === 'audit' && <TeacherStudentAudit student={student} />}
          {activeTab === 'report' && profile.canViewReports && <LessonStudentReports audience="teacher" initialStudent={{ id: student.id, name: student.fullName, phone: student.phone }} />}
        </div>
      </>}
    </div>
  </TeacherPage>;
}
