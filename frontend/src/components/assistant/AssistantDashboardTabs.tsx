'use client';

import React, { useState } from 'react';
import { AssistantTaskBoard } from './AssistantTaskBoard';
import { AssistantOperationsTaskBoard } from './AssistantOperationsTaskBoard';
import { GraduationCap, Briefcase } from 'lucide-react';

export function AssistantDashboardTabs() {
  const [activeTab, setActiveTab] = useState<'academic' | 'operations'>('academic');

  return (
    <div className="mx-auto max-w-6xl space-y-8">
      {/* Tabs Selector */}
      <div className="flex justify-center">
        <div className="inline-flex gap-1 rounded-full border border-[var(--admin-border)] bg-[var(--admin-card)]/90 p-1.5 shadow-sm backdrop-blur-xl">
          <button
            onClick={() => setActiveTab('academic')}
            className={`rounded-full px-6 py-2.5 text-sm font-bold transition flex items-center gap-2 ${
              activeTab === 'academic'
                ? 'bg-[var(--admin-primary)] text-[var(--admin-primary-contrast)] shadow-[0_8px_20px_var(--admin-shadow)]'
                : 'bg-[var(--admin-card-soft)] text-[var(--admin-muted)] hover:text-[var(--admin-text)]'
            }`}
          >
            <GraduationCap className="h-4 w-4" />
            المهام الأكاديمية والطلاب
          </button>
          <button
            onClick={() => setActiveTab('operations')}
            className={`rounded-full px-6 py-2.5 text-sm font-bold transition flex items-center gap-2 ${
              activeTab === 'operations'
                ? 'bg-[var(--admin-primary)] text-[var(--admin-primary-contrast)] shadow-[0_8px_20px_var(--admin-shadow)]'
                : 'bg-[var(--admin-card-soft)] text-[var(--admin-muted)] hover:text-[var(--admin-text)]'
            }`}
          >
            <Briefcase className="h-4 w-4" />
            المهام التشغيلية اليومية
          </button>
        </div>
      </div>

      {/* Tab Panels */}
      {activeTab === 'academic' ? (
        <AssistantTaskBoard />
      ) : (
        <AssistantOperationsTaskBoard />
      )}
    </div>
  );
}
