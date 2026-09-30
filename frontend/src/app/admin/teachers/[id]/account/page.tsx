import { AdminPage } from '@/components/admin';
import { TeacherAccountReport } from '@/features/teacher-finance-center/TeacherAccountReport';

export default async function TeacherAccountPage({ params }: { params: Promise<{ id: string }> }) {
  const { id } = await params;
  return <AdminPage activePath="/admin/platform-profits" sectionLabel="الحسابات" pageTitle="حساب المدرّس" subtitle="المبيعات، نصيب المدرس والمنصة، والمدفوع والمتبقي."><TeacherAccountReport key={id} teacherId={id} /></AdminPage>;
}
