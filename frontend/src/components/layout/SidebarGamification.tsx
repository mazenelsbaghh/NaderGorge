'use client';

import { useEffect, useState } from 'react';
import { Star } from 'lucide-react';
import apiClient from '@/services/api-client';

export function SidebarGamification() {
  const [points, setPoints] = useState<number | null>(null);
  const [level, setLevel] = useState<string>('');
  const [loading, setLoading] = useState(true);

  useEffect(() => {
    async function fetchStatus() {
      try {
        const res = await apiClient.get<{ data: { totalPoints: number; currentLevel: string } }>('/gamification/status');
        setPoints(res.data.data.totalPoints);
        setLevel(res.data.data.currentLevel);
      } catch {
        setPoints(0);
        setLevel('طالب');
      } finally {
        setLoading(false);
      }
    }

    const handleRefresh = () => {
      void fetchStatus();
    };

    window.addEventListener('refresh-student-points', handleRefresh);
    void fetchStatus();

    return () => {
      window.removeEventListener('refresh-student-points', handleRefresh);
    };
  }, []);

  return (
    <div 
      className="group/points relative flex h-12 w-12 group-hover/sidebar:w-full group-hover/sidebar:px-4 flex-col group-hover/sidebar:flex-row items-center justify-center group-hover/sidebar:justify-start gap-2 overflow-hidden rounded-[18px] border border-[var(--admin-primary-15)] bg-[var(--admin-card-soft)] text-[var(--admin-primary)] transition-all hover:bg-[var(--admin-primary-15)] hover:-translate-y-0.5 focus-visible:ring-2 focus-visible:ring-[var(--admin-primary)] focus-visible:ring-offset-2 focus-visible:ring-offset-[var(--admin-sidebar)]"
      title={`نقاطي: ${points || 0}`}
    >
      {loading ? (
        <div className="h-4 w-4 animate-spin rounded-full border-2 border-[var(--admin-primary-strong)] border-r-transparent flex-shrink-0" />
      ) : (
        <>
          <Star className="absolute top-1.5 h-3.5 w-3.5 opacity-50 group-hover:opacity-100 transition-opacity group-hover/sidebar:static group-hover/sidebar:h-5 group-hover/sidebar:w-5 flex-shrink-0 fill-current" />
          <span className="absolute bottom-1.5 font-sans text-[10px] font-black tracking-tighter group-hover/sidebar:static group-hover/sidebar:text-sm flex-shrink-0">
            {points}
          </span>
          <span className="hidden group-hover/sidebar:block text-xs font-bold text-[var(--admin-muted)] truncate whitespace-nowrap mr-auto">
            {level || 'طالب'}
          </span>
        </>
      )}
    </div>
  );
}
