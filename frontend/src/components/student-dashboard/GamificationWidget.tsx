'use client';

import { useEffect, useState } from 'react';
import { Award, Star } from 'lucide-react';
import apiClient from '@/services/api-client';

interface GamificationStatus {
  totalPoints: number;
  currentLevel: string;
  earnedBadges: string[];
}

export function GamificationWidget() {
  const [status, setStatus] = useState<GamificationStatus | null>(null);

  useEffect(() => {
    const fetchStatus = async () => {
      try {
        const res = await apiClient.get<{data: GamificationStatus}>('/gamification/status');
        setStatus(res.data.data);
      } catch (err) {
        console.error('Failed to load gamification status:', err);
      }
    };
    fetchStatus();
  }, []);

  if (!status) return null;

  return (
    <div className="rounded-xl border border-[var(--admin-border)] bg-[var(--admin-card)]/90 backdrop-blur-xl mt-4 rounded-[28px] p-4 bg-gradient-to-br from-[var(--admin-primary)] to-[#b0702d] text-white shadow-lg overflow-hidden relative">
      <div className="absolute -right-4 -top-4 w-24 h-24 bg-white/10 rounded-full blur-2xl"></div>
      
      <div className="flex items-center gap-3 relative z-10">
        <div className="flex shrink-0 h-10 w-10 items-center justify-center rounded-2xl bg-white/20 backdrop-blur-sm text-yellow-300 shadow-inner">
          <Star className="h-5 w-5 fill-yellow-300" />
        </div>
        <div>
          <p className="text-[10px] font-black tracking-[0.2em] text-white/70 uppercase">{status.currentLevel}</p>
          <div className="flex items-baseline gap-1 mt-0.5">
            <span className="text-xl font-black">{status.totalPoints}</span>
            <span className="text-xs font-semibold text-white/80">XP</span>
          </div>
        </div>
      </div>

      {status.earnedBadges.length > 0 && (
         <div className="mt-4 pt-4 border-t border-white/20 relative z-10 flex flex-wrap gap-2">
            {status.earnedBadges.map((badge, idx) => (
                <div key={idx} className="flex items-center gap-1.5 px-2.5 py-1 rounded-xl bg-white/10 backdrop-blur-md border border-white/20 shadow-sm">
                    <Award className="h-3.5 w-3.5 text-yellow-300" />
                    <span className="text-xs font-bold whitespace-nowrap">{badge}</span>
                </div>
            ))}
         </div>
      )}
    </div>
  );
}
