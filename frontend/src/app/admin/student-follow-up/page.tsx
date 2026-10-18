'use client';
import { AdminPage } from '@/components/admin/AdminShellChrome';
import { StudentFollowUp } from '@/components/reports/StudentFollowUp';
export default function AdminStudentFollowUpPage() {
  return <AdminPage activePath="/admin/student-follow-up" sectionLabel="المتابعة الدراسية" pageTitle="متابعة الطلاب" subtitle="اختار المدرس والصف والحصص المطلوبة، واعرف مين لسه محتاج يتابع."><StudentFollowUp audience="admin" /></AdminPage>;
}
