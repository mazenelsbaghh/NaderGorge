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
      <div className="rounded-[2rem] border border-[var(--admin-border)] bg-[var(--admin-card-soft)] p-8">
        <div className="mb-4 flex items-start justify-between">
          <div className="rounded-2xl bg-[var(--admin-primary-15)] p-3 text-[var(--admin-primary)]">
            <Icon className="h-5 w-5" />
          </div>
          <span className="text-[10px] font-bold uppercase tracking-[0.25em] text-[var(--admin-muted)]">
            {label}
          </span>
        </div>
        <div className="text-4xl font-black text-[var(--admin-text)]">{formattedValue}</div>
        {subtitle && (
          <div className="mt-2 text-sm font-semibold text-[var(--admin-muted)]">{subtitle}</div>
        )}
        {children}
      </div>
    );
  }

  if (variant === 'accent') {
    return (
      <div className="rounded-[2rem] bg-[var(--admin-primary-strong)] p-8 text-[#fcd386] shadow-xl">
        <div className="mb-4 flex items-start justify-between">
          <div className="rounded-2xl bg-[var(--admin-primary-contrast)]/15 p-3 text-[#ffdea3]">
            <Icon className="h-5 w-5" />
          </div>
          <span className="text-[10px] font-bold uppercase tracking-[0.25em] text-[#fcd386]/70">
            {label}
          </span>
        </div>
        <div className="text-4xl font-black text-[var(--admin-primary-contrast)]">{formattedValue}</div>
        {subtitle && (
          <div className="mt-2 text-sm font-semibold text-[#fcd386]/80">{subtitle}</div>
        )}
        {children}
      </div>
    );
  }

  // muted
  return (
    <div className="rounded-[2rem] border border-[var(--admin-border)] bg-[var(--admin-card-strong)] p-8">
      <div className="mb-4 flex items-start justify-between">
        <span className="text-[10px] font-bold uppercase tracking-[0.25em] text-[var(--admin-muted)]">
          {label}
        </span>
        <div className="text-[var(--admin-muted)]">
          <Icon className="h-5 w-5" />
        </div>
      </div>
      <div className="text-4xl font-black text-[var(--admin-text)]">{formattedValue}</div>
      {subtitle && (
        <div className="mt-2 text-sm font-semibold text-[var(--admin-muted)]">{subtitle}</div>
      )}
      {children}
    </div>
  );
}
