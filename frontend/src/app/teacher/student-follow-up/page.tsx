'use client';
import { TeacherPage } from '@/components/teacher/TeacherShellChrome';
import { StudentFollowUp } from '@/components/reports/StudentFollowUp';
export default function TeacherStudentFollowUpPage() {
  return <TeacherPage activePath="/teacher/student-follow-up" sectionLabel="المتابعة الدراسية" pageTitle="متابعة الطلاب" subtitle="اعرف طلابك شاهدوا أي حصص، ومين لسه محتاج متابعة، من مكان واحد."><StudentFollowUp audience="teacher" /></TeacherPage>;
}
