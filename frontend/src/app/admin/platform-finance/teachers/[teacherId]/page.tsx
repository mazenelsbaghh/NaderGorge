import { AdminPage } from '@/components/admin';
import TeacherFinancialSummary from '@/components/admin/platform-finance/TeacherFinancialSummary';

export default async function PlatformFinanceTeacherPage({ params }: { params: Promise<{ teacherId: string }> }) { const { teacherId } = await params; return <AdminPage activePath="/admin/platform-finance" sectionLabel="المالية" pageTitle="تفاصيل مالية لمدرس" subtitle="المبيعات والحصة والاسترداد والمدفوع والمتبقي من نفس دفتر الأستاذ."><TeacherFinancialSummary teacherId={teacherId} /></AdminPage>; }
