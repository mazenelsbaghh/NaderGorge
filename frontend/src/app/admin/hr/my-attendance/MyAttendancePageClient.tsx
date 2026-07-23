'use client';
import { AdminShellChrome } from '@/components/admin';
import { AttendanceWorkspace } from '@/features/hr/attendance';
export default function MyAttendancePageClient() { return <AdminShellChrome activePath="/admin/hr/my-attendance" sectionLabel="الموارد البشرية" pageTitle="حضوري" subtitle="الحضور والانصراف والاستراحات حسب سياسة شفتك."><AttendanceWorkspace /></AdminShellChrome>; }
