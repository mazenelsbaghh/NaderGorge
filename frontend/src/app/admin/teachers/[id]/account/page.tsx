import { AdminPage } from '@/components/admin';
import { TeacherAccountReport } from '@/features/teacher-finance-center/TeacherAccountReport';

export default async function TeacherAccountPage({ params }: { params: Promise<{ id: string }> }) {
  const { id } = await params;
  return <AdminPage activePath="/admin/teachers" sectionLabel="المدرسون" pageTitle="كشف حساب المدرس" subtitle="الأرباح والأرصدة وحالة تسوية كل حركة."><TeacherAccountReport key={id} teacherId={id} /></AdminPage>;
}
