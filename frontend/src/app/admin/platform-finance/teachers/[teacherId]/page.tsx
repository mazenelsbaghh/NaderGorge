import { AdminPage } from '@/components/admin';
import TeacherFinancialSummary from '@/components/admin/platform-finance/TeacherFinancialSummary';

export default async function PlatformFinanceTeacherPage({ params }: { params: Promise<{ teacherId: string }> }) { const { teacherId } = await params; return <AdminPage activePath="/admin/platform-finance" sectionLabel="المالية" pageTitle="تفاصيل مالية لمدرس" subtitle="الأرباح، المدفوع، والمتاح للسحب من حساب المدرّس."><TeacherFinancialSummary teacherId={teacherId} /></AdminPage>; }
