'use client';
import { AdminPage } from '@/components/admin';
import { AttendanceCorrectionReview } from '@/features/hr/attendance';
export default function HrAttendanceCorrectionsPageClient() { return <AdminPage activePath="/admin/hr/attendance-corrections" sectionLabel="الموارد البشرية" pageTitle="تصحيحات الحضور" subtitle="مقارنة قبل وبعد واعتماد المدير ثم HR."><AttendanceCorrectionReview /></AdminPage>; }
