'use client';
import { AdminShellChrome } from '@/components/admin';
import { AttendanceCorrectionReview } from '@/features/hr/attendance';
export default function HrAttendanceCorrectionsPageClient() { return <AdminShellChrome activePath="/admin/hr/attendance-corrections" sectionLabel="الموارد البشرية" pageTitle="تصحيحات الحضور" subtitle="مقارنة قبل وبعد واعتماد المدير ثم HR."><AttendanceCorrectionReview /></AdminShellChrome>; }
