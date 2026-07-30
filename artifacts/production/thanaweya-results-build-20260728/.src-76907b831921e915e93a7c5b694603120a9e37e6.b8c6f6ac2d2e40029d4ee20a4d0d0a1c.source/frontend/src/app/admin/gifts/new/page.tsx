import { AdminShellChrome } from '@/components/admin';
import { GiftIssueForm } from '@/components/admin/gifts/GiftIssueForm';
export default function NewGiftPage() { return <AdminShellChrome activePath="/admin/gifts" sectionLabel="الهدايا" pageTitle="إصدار هدية جديدة" subtitle="اختر هدفاً واحداً، المستفيدين، وحدود الاستخدام قبل التأكيد."><GiftIssueForm /></AdminShellChrome>; }
