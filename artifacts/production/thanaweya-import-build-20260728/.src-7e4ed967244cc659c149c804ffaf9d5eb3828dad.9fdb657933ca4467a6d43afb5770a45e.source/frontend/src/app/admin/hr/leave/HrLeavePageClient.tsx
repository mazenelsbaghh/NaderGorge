'use client';
import { AdminShellChrome } from '@/components/admin';
import { LeaveAdministration } from '@/features/hr/leave';
export default function HrLeavePageClient() { return <AdminShellChrome activePath="/admin/hr/leave" sectionLabel="الموارد البشرية" pageTitle="سياسات الإجازات" subtitle="أنواع الإجازات والاستحقاقات والأرصدة المرتبطة بتقويم العمل."><LeaveAdministration /></AdminShellChrome>; }
