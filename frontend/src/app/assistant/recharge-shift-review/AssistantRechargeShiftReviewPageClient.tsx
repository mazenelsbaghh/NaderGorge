'use client';
import { RechargeShiftReviewWorkspace } from '@/app/admin/recharge-shift-review/RechargeShiftReviewPageClient';
import { AssistantPage } from '@/components/assistant/AssistantShellChrome';
import { NavRouteGuard } from '@/components/layout/NavRouteGuard';
export default function AssistantRechargeShiftReviewPageClient() { return <NavRouteGuard routePath="/assistant/recharge-shift-review" permission="payments.manage"><AssistantPage activePath="/assistant/recharge-shift-review" sectionLabel="المدفوعات" pageTitle="مراجعة شحن آخر الشيفت" subtitle="راجع المقبول يدويًا وآليًا ونزّل شيت Excel قبل تسليم الشيفت."><RechargeShiftReviewWorkspace /></AssistantPage></NavRouteGuard>; }
