import { AdminPage } from '@/components/admin';
import { GiftIssueForm } from '@/components/admin/gifts/GiftIssueForm';
export default function NewGiftPage() { return <AdminPage activePath="/admin/gifts" sectionLabel="الهدايا" pageTitle="إصدار هدية جديدة" subtitle="اختر هدفاً واحداً، المستفيدين، وحدود الاستخدام قبل التأكيد."><GiftIssueForm /></AdminPage>; }
