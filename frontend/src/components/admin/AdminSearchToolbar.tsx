import React from 'react';
import { Search } from 'lucide-react';

export interface AdminSearchToolbarProps {
  value: string;
  onChange: (value: string) => void;
  placeholder?: string;
  actions?: React.ReactNode;
}

export function AdminSearchToolbar({
  value,
  onChange,
  placeholder = 'ابحث...',
  actions,
}: AdminSearchToolbarProps) {
  return (
    <div className="mb-6 flex flex-wrap items-center justify-between gap-4 rounded-3xl bg-[var(--admin-card-soft)] p-4 border border-[var(--admin-border)]">
      <div className="relative w-full md:max-w-md">
        <input
          type="text"
          value={value}
          onChange={(e) => onChange(e.target.value)}
          placeholder={placeholder}
          className="admin-input py-3 pl-4 pr-12 text-sm font-bold"
        />
        <Search className="absolute right-4 top-1/2 h-5 w-5 -translate-y-1/2 text-[var(--admin-muted)]" />
      </div>
      
      {actions && <div className="flex items-center gap-3">{actions}</div>}
    </div>
  );
}
