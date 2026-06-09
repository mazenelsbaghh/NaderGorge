'use client';

import React, { useState } from 'react';
import { 
  Calendar, 
  BarChart3
} from 'lucide-react';
import { AdminShellChrome } from '@/components/admin';
import MediaPipelineBoard from '@/components/media/MediaPipelineBoard';
import SocialPlannerView from '@/components/media/SocialPlannerView';
import MediaKpiDashboard from '@/components/media/MediaKpiDashboard';

const TrelloIcon = (props: any) => (
  <svg viewBox="0 0 24 24" width="24" height="24" stroke="currentColor" strokeWidth="2" fill="none" strokeLinecap="round" strokeLinejoin="round" {...props}>
    <rect x="3" y="3" width="18" height="18" rx="2" ry="2" />
    <rect x="7" y="7" width="3" height="9" rx="1" />
    <rect x="14" y="7" width="3" height="5" rx="1" />
  </svg>
);

type MediaTab = 'board' | 'planner' | 'kpis';

export default function AdminMediaPage() {
  const [activeTab, setActiveTab] = useState<MediaTab>('board');

  const tabs: { id: MediaTab; label: string; icon: any }[] = [
    { id: 'board', label: 'لوحة تتبع الإنتاج المرئي', icon: TrelloIcon },
    { id: 'planner', label: 'مخطط النشر الرقمي', icon: Calendar },
    { id: 'kpis', label: 'مؤشرات الأداء والتقارير', icon: BarChart3 }
  ];

  return (
    <AdminShellChrome
      activePath="/admin/media"
      sectionLabel="إنتاج المحتوى والمنشورات"
      pageTitle="إدارة مسار الإنتاج والمنشورات"
      subtitle="تنسيق وجدولة المواد المرئية ومنشورات التواصل الاجتماعي ومتابعة مراحل التصوير والمونتاج والمراجعة والجودة."
    >
      <div className="flex flex-col gap-6" dir="rtl">
        {/* Tab Selection */}
        <div className="flex items-center gap-1.5 p-1 rounded-2xl bg-[var(--admin-card-soft)] border border-[var(--admin-border)] w-fit backdrop-blur-md">
          {tabs.map((tab) => {
            const Icon = tab.icon;
            const isActive = activeTab === tab.id;

            return (
              <button
                key={tab.id}
                onClick={() => setActiveTab(tab.id)}
                className={`flex items-center gap-2 px-4 py-2 text-xs font-bold rounded-xl transition-all cursor-pointer ${
                  isActive
                    ? 'bg-[var(--admin-primary)] text-[var(--admin-primary-contrast)] shadow-sm'
                    : 'text-[var(--admin-muted)] hover:text-[var(--admin-text)] hover:bg-[var(--admin-hover)]'
                }`}
              >
                <Icon className="h-4 w-4" />
                <span>{tab.label}</span>
              </button>
            );
          })}
        </div>

        {/* Tab Content Panels */}
        <div className="w-full transition-opacity duration-300">
          {activeTab === 'board' && <MediaPipelineBoard />}
          {activeTab === 'planner' && <SocialPlannerView />}
          {activeTab === 'kpis' && <MediaKpiDashboard />}
        </div>
      </div>
    </AdminShellChrome>
  );
}
