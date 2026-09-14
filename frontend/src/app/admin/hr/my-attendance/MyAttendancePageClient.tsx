'use client';
import { AdminPage } from '@/components/admin';
import { AttendanceWorkspace } from '@/features/hr/attendance';
export default function MyAttendancePageClient() { return <AdminPage activePath="/admin/hr/my-attendance" sectionLabel="الموارد البشرية" pageTitle="حضوري" subtitle="الحضور والانصراف والاستراحات حسب سياسة شفتك."><AttendanceWorkspace /></AdminPage>; }
