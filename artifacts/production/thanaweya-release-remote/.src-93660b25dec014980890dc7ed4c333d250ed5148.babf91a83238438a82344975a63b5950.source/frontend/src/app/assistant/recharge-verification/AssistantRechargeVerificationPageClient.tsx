'use client';

import { AssistantShellChrome } from '@/components/assistant/AssistantShellChrome';
import { NavRouteGuard } from '@/components/layout/NavRouteGuard';
import { RechargeVerificationWorkspace } from '@/app/admin/recharge-verification/RechargeVerificationPageClient';

export default function AssistantRechargeVerificationPageClient() {
  return (
    <NavRouteGuard routePath="/assistant/recharge-verification" permission="payments.manage">
      <AssistantShellChrome
        activePath="/assistant/recharge-verification"
        sectionLabel="المدفوعات"
        pageTitle="مطابقة الشحن"
        subtitle="راجع إثباتات التحويل، واربطها برسائل التأكيد، ثم اقبل أو ارفض الطلب بأمان."
      >
        <RechargeVerificationWorkspace />
      </AssistantShellChrome>
    </NavRouteGuard>
  );
}
