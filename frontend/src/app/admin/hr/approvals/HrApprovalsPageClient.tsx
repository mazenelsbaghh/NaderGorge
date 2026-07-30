'use client';
import { AdminPage } from '@/components/admin';
import { ApprovalDefinitionEditor, ApprovalInbox } from '@/features/hr/leave';
export default function HrApprovalsPageClient() { return <AdminPage activePath="/admin/hr/approvals" sectionLabel="الموارد البشرية" pageTitle="صندوق الموافقات" subtitle="الموافقات الحالية والتفويض والتصعيد دون السماح باعتماد الطلب الشخصي."><div className="space-y-5"><ApprovalDefinitionEditor /><ApprovalInbox /></div></AdminPage>; }
