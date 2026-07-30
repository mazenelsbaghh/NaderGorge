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
  const [hasError, setHasError] = useState(false);

  useEffect(() => {
    const fetchStatus = async () => {
      try {
        const res = await apiClient.get<{data: GamificationStatus}>('/gamification/status');
        setStatus(res.data.data);
        setHasError(false);
      } catch {
        setHasError(true);
      }
    };
    void fetchStatus();
  }, []);

  if (!status) {
    if (hasError) {
      return (
        <div className="mt-4 rounded-[28px] border border-[var(--admin-border)] bg-[var(--admin-card)] p-4 text-sm font-bold text-[var(--admin-muted)] shadow-sm">
          تعذر تحميل نقاطك الآن.
        </div>
      );
    }

    return null;
  }

  return (
    <div className="mt-4 rounded-[28px] border border-[var(--admin-border)] bg-[var(--admin-card)] p-4 shadow-sm">
      <div className="flex items-center gap-3">
        <div className="flex h-10 w-10 shrink-0 items-center justify-center rounded-2xl bg-[var(--admin-primary-15)] text-[var(--admin-primary)]">
          <Star className="h-5 w-5 fill-current" />
        </div>
        <div>
          <p className="text-xs font-black uppercase tracking-[0.2em] text-[var(--admin-primary)]">{status.currentLevel}</p>
          <div className="mt-0.5 flex items-baseline gap-1">
            <span className="text-xl font-black text-[var(--admin-text)]">{status.totalPoints}</span>
            <span className="text-xs font-semibold text-[var(--admin-muted)]">نقطة</span>
          </div>
        </div>
      </div>

      {status.earnedBadges.length > 0 && (
         <div className="mt-4 flex flex-wrap gap-2 border-t border-[var(--admin-border)] pt-4">
            {status.earnedBadges.map((badge, idx) => (
                <div key={idx} className="flex items-center gap-1.5 rounded-xl bg-[var(--admin-card-soft)] px-2.5 py-1">
                    <Award className="h-3.5 w-3.5 text-[var(--admin-primary)]" />
                    <span className="whitespace-nowrap text-xs font-bold text-[var(--admin-text)]">{badge}</span>
                </div>
            ))}
         </div>
      )}
    </div>
  );
}
