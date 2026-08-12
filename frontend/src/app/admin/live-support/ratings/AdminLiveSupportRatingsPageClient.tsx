'use client';

import { useState } from 'react';
import { AdminPage } from '@/components/admin/AdminShellChrome';
import { ConversationInvestigation } from '@/components/live-support/admin/ConversationInvestigation';
import { LiveSupportRatingsPanel } from '@/components/live-support/admin/LiveSupportRatingsPanel';
import { liveSupportService, type LiveSupportConversationTimeline } from '@/services/live-support-service';

export default function AdminLiveSupportRatingsPageClient() {
  const [timeline, setTimeline] = useState<LiveSupportConversationTimeline>();
  return <AdminPage activePath="/admin/live-support/ratings" sectionLabel="الاتصال والتواصل" pageTitle="تقييمات الدعم المباشر" subtitle="راجع نجوم الطلاب وتعليقاتهم خلال الفترة التي تحددها."><div dir="rtl"><LiveSupportRatingsPanel openConversation={(conversationId) => void liveSupportService.getAdminTimeline(conversationId).then(setTimeline)} /></div>{timeline && <ConversationInvestigation timeline={timeline} close={() => setTimeline(undefined)} />}</AdminPage>;
}
