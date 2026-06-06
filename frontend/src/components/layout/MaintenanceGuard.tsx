'use client';

import { useEffect, useState } from 'react';
import { useAuthStore } from '@/stores/auth-store';
import { Wrench } from 'lucide-react';
import apiClient from '@/services/api-client';
import Image from 'next/image';

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
      <div dir="rtl" className="flex min-h-dvh flex-col items-center justify-center bg-[#050e1a] px-6 text-slate-100 font-[family-name:var(--font-tajawal)] overflow-hidden relative">
        {/* Modern ambient branding glow elements */}
        <div className="absolute top-[-10%] right-[-10%] w-[50%] h-[50%] rounded-full bg-[#0E8F8F]/15 blur-[120px] pointer-events-none" />
        <div className="absolute bottom-[-10%] left-[-10%] w-[50%] h-[50%] rounded-full bg-[#D4A017]/10 blur-[120px] pointer-events-none" />
        
        {/* Brand Logo Header */}
        <div className="relative mb-8 text-center animate-fade-in">
          <Image 
            src="/images/logo-mark-light.svg" 
            width={80}
            height={80}
            className="h-20 w-auto mx-auto drop-shadow-[0_0_15px_rgba(14,143,143,0.3)]" 
            alt="مسار أكاديمي" 
            priority
          />
        </div>

        {/* Premium Glassmorphism Card */}
        <div className="relative max-w-lg w-full overflow-hidden rounded-[32px] bg-slate-900/60 border border-white/10 p-8 sm:p-10 text-center shadow-[0_24px_70px_rgba(0,0,0,0.5)] backdrop-blur-2xl">
          {/* Internal card gold highlight glow */}
          <div className="pointer-events-none absolute inset-0 bg-[radial-gradient(circle_at_50%_0%,rgba(212,160,23,0.06),transparent_70%)]" />
          
          <div className="relative flex justify-center mb-6">
            <div className="p-4.5 bg-[#0E8F8F]/10 rounded-2xl text-[#0E8F8F] border border-[#0E8F8F]/20 shadow-[0_8px_30px_rgba(14,143,143,0.15)]">
              <Wrench className="w-10 h-10 animate-bounce" style={{ animationDuration: '3s' }} />
            </div>
          </div>

          <h1 className="relative text-2xl font-black mb-4 text-[#D4A017] tracking-tight">أعمال صيانة مجدولة</h1>
          
          <p className="relative text-slate-300 leading-relaxed mb-6 font-medium text-[15px] px-2">
            {maintenanceMessage}
          </p>

          <div className="relative pt-5 border-t border-white/5 flex flex-col items-center justify-center gap-1.5 text-xs text-slate-400 font-bold">
            <span className="text-[#0E8F8F]">مسار أكاديمي — شريك تفوقك الدراسي</span>
            <span className="opacity-75 font-normal">شكراً لتفهمكم ونعتذر عن هذا العطل المؤقت.</span>
          </div>
        </div>
      </div>
    );
  }

  return <>{children}</>;
}
