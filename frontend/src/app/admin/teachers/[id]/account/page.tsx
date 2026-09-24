import { AdminPage } from '@/components/admin';
import { TeacherAccountReport } from '@/features/teacher-finance-center/TeacherAccountReport';

export default async function TeacherAccountPage({ params }: { params: Promise<{ id: string }> }) {
  const { id } = await params;
  return <AdminPage activePath="/admin/teachers" sectionLabel="المدرسون" pageTitle="حساب المدرّس" subtitle="ربحه جاي منين، اتحسب إزاي، استلم كام، ومتاح له يسحب كام."><TeacherAccountReport key={id} teacherId={id} /></AdminPage>;
}
