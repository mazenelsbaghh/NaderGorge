'use client';
import { RechargeConflictsWorkspace } from '@/app/admin/recharge-conflicts/RechargeConflictsPageClient';
import { AssistantPage } from '@/components/assistant/AssistantShellChrome';
import { NavRouteGuard } from '@/components/layout/NavRouteGuard';
export default function AssistantRechargeConflictsPageClient() { return <NavRouteGuard routePath="/assistant/recharge-conflicts" permission="payments.manage"><AssistantPage activePath="/assistant/recharge-conflicts" sectionLabel="المدفوعات" pageTitle="تعارضات رسائل الشحن" subtitle="راجع الحساب الكامل ورقم العملية قبل نقل أي رسالة."><RechargeConflictsWorkspace /></AssistantPage></NavRouteGuard>; }
