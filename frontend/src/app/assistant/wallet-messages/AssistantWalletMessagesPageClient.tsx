'use client';

import { WalletMessagesWorkspace } from '@/app/admin/wallet-messages/WalletMessagesPageClient';
import { AssistantPage } from '@/components/assistant/AssistantShellChrome';
import { NavRouteGuard } from '@/components/layout/NavRouteGuard';

export default function AssistantWalletMessagesPageClient() {
  return (
    <NavRouteGuard routePath="/assistant/wallet-messages" permission="payments.manage">
      <AssistantPage
        activePath="/assistant/wallet-messages"
        sectionLabel="المدفوعات"
        pageTitle="رسائل المحافظ"
        subtitle="ابحث في كل رسائل المحافظ وراجع حالة مطابقتها بطلبات الشحن."
      >
        <WalletMessagesWorkspace rechargePath="/assistant/recharge-verification" />
      </AssistantPage>
    </NavRouteGuard>
  );
}
