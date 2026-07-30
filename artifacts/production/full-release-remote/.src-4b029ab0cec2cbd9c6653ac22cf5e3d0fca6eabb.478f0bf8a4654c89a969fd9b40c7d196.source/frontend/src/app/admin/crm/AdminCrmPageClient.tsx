'use client';

import { useState } from 'react';
import { AdminShellChrome } from '@/components/admin';
import { CrmStudentQueue } from '@/components/crm/CrmStudentQueue';
import { CrmReportsPanel } from '@/components/crm/CrmReportsPanel';
import { BarChart2, ListTodo } from 'lucide-react';

export default function AdminCrmPageClient() {
  const [activeTab, setActiveTab] = useState<'queue' | 'reports'>('queue');

  return (
    <AdminShellChrome
      activePath="/admin/crm"
      sectionLabel="الكول سنتر والمتابعة"
      pageTitle="إدارة علاقات الطلاب والاتصالات"
      subtitle="إدارة وتوزيع قوائم الطلاب على موظفي الكول سنتر ومتابعة تقارير الاتصال اليومية."
    >
      <div className="space-y-8 animate-[fadeIn_0.4s_ease-out]" dir="rtl">
        {/* Navigation Tabs */}
        <div className="flex justify-start">
          <div className="inline-flex gap-1 rounded-full border border-[var(--admin-border)] bg-[var(--admin-card)]/90 p-1.5 shadow-sm backdrop-blur-xl">
            <button
              onClick={() => setActiveTab('queue')}
              className={`rounded-full px-6 py-2.5 text-xs font-bold transition flex items-center gap-2 ${
                activeTab === 'queue'
                  ? 'bg-[var(--admin-primary)] text-[var(--admin-primary-contrast)] shadow-[0_8px_20px_var(--admin-shadow)]'
                  : 'bg-[var(--admin-card-soft)] text-[var(--admin-muted)] hover:text-[var(--admin-text)]'
              }`}
            >
              <ListTodo className="h-4 w-4" />
              قوائم التوزيع والمتابعة
            </button>
            <button
              onClick={() => setActiveTab('reports')}
              className={`rounded-full px-6 py-2.5 text-xs font-bold transition flex items-center gap-2 ${
                activeTab === 'reports'
                  ? 'bg-[var(--admin-primary)] text-[var(--admin-primary-contrast)] shadow-[0_8px_20px_var(--admin-shadow)]'
                  : 'bg-[var(--admin-card-soft)] text-[var(--admin-muted)] hover:text-[var(--admin-text)]'
              }`}
            >
              <BarChart2 className="h-4 w-4" />
              تقارير الأداء والإحصائيات
            </button>
          </div>
        </div>

        {/* Tab View Panels */}
        {activeTab === 'queue' ? (
          <CrmStudentQueue mode="admin" />
        ) : (
          <CrmReportsPanel />
        )}
      </div>
    </AdminShellChrome>
  );
}
