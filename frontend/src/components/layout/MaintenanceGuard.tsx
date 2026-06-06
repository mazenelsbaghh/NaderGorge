'use client';

import { useEffect, useState } from 'react';
import { useAuthStore } from '@/stores/auth-store';
import { Wrench } from 'lucide-react';
import apiClient from '@/services/api-client';

export function MaintenanceGuard({ children }: { children: React.ReactNode }) {
  const { user } = useAuthStore();
  const [isMaintenance, setIsMaintenance] = useState(false);
  const [maintenanceMessage, setMaintenanceMessage] = useState('المنصة في أعمال الصيانة حالياً، سنعود قريباً.');
  const [loading, setLoading] = useState(true);

  useEffect(() => {
    let active = true;
    apiClient.get('/public/settings')
      .then((res) => {
        if (!active) return;
        const data = res.data;
        if (data.maintenanceMode) {
          setIsMaintenance(true);
          if (data.maintenanceMessage) {
            setMaintenanceMessage(data.maintenanceMessage);
          }
        }
        setLoading(false);
      })
      .catch(() => {
        if (active) setLoading(false);
      });

    return () => {
      active = false;
    };
  }, []);

  const isStaff = user?.roles?.some((role) => ['Admin', 'Teacher'].includes(role));

  if (loading) {
    return (
      <div dir="rtl" className="flex min-h-dvh items-center justify-center bg-[var(--admin-bg)] px-6 text-[var(--admin-text)]">
        <div className="relative overflow-hidden rounded-[24px] border border-[var(--admin-border)] bg-[var(--admin-card)] px-6 py-5 text-center shadow-[0_18px_48px_var(--admin-shadow)]">
          <div className="w-6 h-6 border-2 border-[var(--admin-primary)] border-t-transparent rounded-full animate-spin mx-auto mb-2"></div>
          <p className="text-sm font-bold text-[var(--admin-muted)]">جاري التحقق من حالة المنصة...</p>
        </div>
      </div>
    );
  }

  if (isMaintenance && !isStaff) {
    return (
      <div dir="rtl" className="flex min-h-dvh flex-col items-center justify-center bg-[#faf2e6] px-6 text-[#2c1708] font-[family-name:var(--font-tajawal)]">
        <div className="relative max-w-md w-full overflow-hidden rounded-[32px] bg-[#fcf6ea] p-8 text-center shadow-[0_24px_60px_rgba(154,105,51,0.12)] border border-[#f0e4ce]">
          {/* Subtle gold gradient background glow */}
          <div className="pointer-events-none absolute inset-0 bg-[radial-gradient(circle_at_50%_0%,rgba(154,105,51,0.08),transparent_70%)]" />
          
          <div className="relative flex justify-center mb-6">
            <div className="p-4 bg-[#f2dfbc] rounded-2xl text-[#9a6933] shadow-[0_8px_20px_rgba(154,105,51,0.1)]">
              <Wrench className="w-12 h-12 animate-pulse" />
            </div>
          </div>

          <h1 className="relative text-2xl font-[900] mb-4 text-[#7f5427] tracking-tight">أعمال صيانة مجدولة</h1>
          
          <p className="relative text-[#7a644d] leading-relaxed mb-6 font-medium text-[15px]">
            {maintenanceMessage}
          </p>

          <div className="relative pt-4 border-t border-[#f0e4ce] flex items-center justify-center gap-2 text-xs text-[#7a644d] font-bold">
            <span>شكراً لتفهمكم ونعتذر عن هذا العطل المؤقت.</span>
          </div>
        </div>
      </div>
    );
  }

  return <>{children}</>;
}
