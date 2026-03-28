import React from 'react';
import { LucideIcon } from 'lucide-react';
import { formatCompactNumber } from './admin-utils';

export type AdminStatCardVariant = 'light' | 'accent' | 'muted';

export interface AdminStatCardProps {
  icon: LucideIcon;
  label: string;
  value: string | number;
  variant: AdminStatCardVariant;
  subtitle?: string;
  children?: React.ReactNode;
}

export function AdminStatCard({
  icon: Icon,
  label,
  value,
  variant,
  subtitle,
  children,
}: AdminStatCardProps) {
  const formattedValue = typeof value === 'number' ? formatCompactNumber(value) : value;

  if (variant === 'light') {
    return (
      <div className="group relative overflow-hidden rounded-[2.5rem] border border-[var(--admin-border)] bg-[var(--admin-card-soft)] p-6 shadow-sm transition-all duration-500 hover:-translate-y-1 hover:shadow-xl dark:bg-[var(--admin-card)] dark:shadow-none dark:hover:border-[var(--admin-primary-15)]">
        <div className="absolute -right-8 -top-8 text-[var(--admin-primary-15)] transition-transform duration-700 group-hover:rotate-12 group-hover:scale-125 dark:opacity-50 pointer-events-none">
          <Icon className="h-40 w-40" />
        </div>
        
        <div className="relative z-10 flex h-full flex-col justify-between gap-8">
          <div className="flex items-center gap-4">
             <div className="flex h-12 w-12 shrink-0 items-center justify-center rounded-2xl bg-[var(--admin-primary)] text-[var(--admin-primary-contrast)] shadow-lg shadow-[var(--admin-primary-15)] transition-transform duration-300 group-hover:scale-110">
               <Icon className="h-5 w-5" />
             </div>
             <span className="text-base font-bold leading-tight text-[var(--admin-text)] group-hover:text-[var(--admin-primary)] transition-colors">
               {label}
             </span>
          </div>
          
          <div>
            <div className="text-5xl font-black tracking-tighter text-[var(--admin-primary)] drop-shadow-sm">{formattedValue}</div>
            {subtitle && (
              <div className="mt-2 text-sm font-bold text-[var(--admin-muted)]/80">{subtitle}</div>
            )}
            {children}
          </div>
        </div>
      </div>
    );
  }

  if (variant === 'accent') {
    return (
      <div className="group relative overflow-hidden rounded-[2.5rem] bg-gradient-to-br from-[var(--admin-primary)] to-[var(--admin-primary-strong)] p-6 shadow-2xl transition-all duration-500 hover:-translate-y-1 hover:shadow-[var(--admin-primary-15)]">
        {/* Subtle noise/texture overlay (using admin primary tint if no image exists, or a subtle pattern) */}
        <div className="absolute inset-0 bg-[radial-gradient(circle_at_30%_50%,var(--admin-card-soft),transparent_70%)] opacity-[0.08] mix-blend-overlay pointer-events-none"></div>
        <div className="absolute -bottom-10 -left-10 text-[var(--admin-primary-contrast)] opacity-[0.08] transition-transform duration-700 group-hover:-rotate-12 group-hover:scale-125 pointer-events-none">
          <Icon className="h-48 w-48" />
        </div>

        <div className="relative z-10 flex h-full flex-col justify-between gap-8">
          <div className="flex items-start justify-between">
            <span className="text-xl font-black text-[var(--admin-primary-contrast)]/90 drop-shadow-md">
              {label}
            </span>
            <div className="flex h-10 w-10 shrink-0 items-center justify-center rounded-full bg-[var(--admin-primary-contrast)]/20 text-[var(--admin-primary-contrast)] backdrop-blur-md transition-transform duration-300 group-hover:scale-110">
               <Icon className="h-4 w-4" />
            </div>
          </div>
          
          <div>
            <div className="text-6xl font-black tracking-tighter text-[var(--admin-primary-contrast)] drop-shadow-lg">
              {formattedValue}
            </div>
            {subtitle && (
              <div className="mt-3 inline-flex items-center rounded-xl bg-[var(--admin-primary-contrast)]/10 px-3 py-1 text-xs font-bold text-[var(--admin-primary-contrast)] backdrop-blur-sm border border-[var(--admin-primary-contrast)]/20 shadow-inner">
                {subtitle}
              </div>
            )}
            {children}
          </div>
        </div>
      </div>
    );
  }

  // muted
  return (
    <div className="group relative overflow-hidden rounded-[2.5rem] border border-[var(--admin-border)] bg-[var(--admin-card-strong)] p-6 shadow-sm transition-colors duration-500 hover:bg-[var(--admin-card)] dark:bg-[var(--admin-background)] dark:hover:bg-[var(--admin-card)]">
      <div className="relative z-10 flex h-full flex-col justify-between gap-6">
        <div className="flex items-start justify-between">
          <span className="text-sm font-extrabold text-[var(--admin-muted)] group-hover:text-[var(--admin-text)] transition-colors">
            {label}
          </span>
          <div className="text-[var(--admin-muted)] transition-all duration-300 group-hover:scale-110 group-hover:text-[var(--admin-primary)]">
            <Icon className="h-5 w-5" />
          </div>
        </div>
        
        <div>
          <div className="text-4xl font-black tracking-tight text-[var(--admin-text)]">
            {formattedValue}
          </div>
          {subtitle && (
            <div className="mt-2 text-xs font-bold text-[var(--admin-muted)]/70">{subtitle}</div>
          )}
          {children}
        </div>
      </div>
    </div>
  );
}
