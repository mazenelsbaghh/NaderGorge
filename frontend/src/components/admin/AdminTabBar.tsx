import React from 'react';
import { LucideIcon } from 'lucide-react';

export interface AdminTab<T extends string> {
  key: T;
  label: string;
  icon?: LucideIcon;
}

export interface AdminTabBarProps<T extends string> {
  tabs: AdminTab<T>[];
  activeTab: T;
  onSelect: (tab: T) => void;
}

export function AdminTabBar<T extends string>({
  tabs,
  activeTab,
  onSelect,
}: AdminTabBarProps<T>) {
  return (
    <div className="flex w-full md:w-fit items-center gap-2 overflow-x-auto rounded-2xl md:rounded-full border border-[var(--admin-border)] bg-[var(--admin-card)]/90 p-2 backdrop-blur-md [-ms-overflow-style:none] [scrollbar-width:none] [&::-webkit-scrollbar]:hidden">
      {tabs.map((tab) => {
        const isActive = activeTab === tab.key;
        const Icon = tab.icon;
        return (
          <button
            key={tab.key}
            onClick={() => onSelect(tab.key)}
            className={`flex items-center gap-2 whitespace-nowrap rounded-full px-5 py-2.5 text-sm font-bold transition-all flex-shrink-0 ${
              isActive
                ? 'bg-[var(--admin-primary-strong)] text-[var(--admin-primary-contrast)] shadow-md'
                : 'bg-[var(--admin-card-soft)] text-[var(--admin-muted)] hover:bg-[var(--admin-hover)]'
            }`}
          >
            {Icon && <Icon className="h-4 w-4" />}
            {tab.label}
          </button>
        );
      })}
    </div>
  );
}
