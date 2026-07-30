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
  const isAccent = variant === 'accent';
  const isMuted = variant === 'muted';

  return (
    <section
      className={`rounded-2xl border p-5 transition-colors duration-200 ${
        isAccent
          ? 'border-[var(--admin-primary)] bg-[var(--admin-primary)] text-[var(--admin-primary-contrast)]'
          : isMuted
            ? 'border-[var(--admin-border)] bg-[var(--admin-card-soft)]'
            : 'border-[var(--admin-border)] bg-[var(--admin-card)]'
      }`}
    >
      <div className="flex items-center justify-between gap-4">
        <span className={`text-sm font-bold ${isAccent ? 'text-[var(--admin-primary-contrast)]' : 'text-[var(--admin-muted)]'}`}>
          {label}
        </span>
        <span className={`flex h-10 w-10 shrink-0 items-center justify-center rounded-xl ${isAccent ? 'bg-[var(--admin-primary-contrast)]/15 text-[var(--admin-primary-contrast)]' : 'bg-[var(--admin-primary-15)] text-[var(--admin-primary)]'}`}>
          <Icon className="h-5 w-5" aria-hidden="true" />
        </span>
      </div>
      <div className="mt-7">
        <div className={`text-4xl font-black leading-none tabular-nums ${isAccent ? 'text-[var(--admin-primary-contrast)]' : 'text-[var(--admin-text)]'}`}>
          {formattedValue}
        </div>
        {subtitle && (
          <p className={`mt-2 text-sm font-medium ${isAccent ? 'text-[var(--admin-primary-contrast)]/80' : 'text-[var(--admin-muted)]'}`}>
            {subtitle}
          </p>
        )}
        {children}
      </div>
    </section>
  );
}
