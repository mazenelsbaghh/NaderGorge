'use client';

import React, { useState, useEffect } from 'react';
import { ChevronRight, ChevronLeft } from 'lucide-react';
import { formatCompactNumber } from './admin-utils';

export interface AdminColumn<T> {
  key: string;
  label: string;
  render: (row: T) => React.ReactNode;
  align?: 'right' | 'left' | 'center';
}

/**
 * Props for the generic Admin data table.
 */
export interface AdminDataTableProps<T> {
  data: T[];
  columns: AdminColumn<T>[];
  loading?: boolean;
  rowKey: (item: T) => string | number;
  emptyMessage?: string;
  pageSize?: number;
  expandedRowRender?: (record: T) => React.ReactNode;
  onRowClick?: (record: T) => void;
}

/**
 * Generates a unified data table with sorting, pagination, and theming.
 * Ensure to provide a generic type `<T>` for proper typing of `data` and `columns`.
 *
 * @param props Props containing data, columns, pagination constraints, etc.
 * @returns DataTable TSX component
 */
export function AdminDataTable<T>({
  data,
  columns,
  loading = false,
  pageSize = 8,
  emptyMessage = 'لا توجد نتائج.',
  rowKey,
  expandedRowRender,
  onRowClick,
}: AdminDataTableProps<T>) {
  const [page, setPage] = useState(1);
  const [expandedKeys, setExpandedKeys] = useState<Set<string | number>>(new Set());

  const toggleExpand = (key: string | number) => {
    const newKeys = new Set(expandedKeys);
    if (newKeys.has(key)) {
      newKeys.delete(key);
    } else {
      newKeys.add(key);
    }
    setExpandedKeys(newKeys);
  };

  // Reset page when data changes
  useEffect(() => {
    setPage(1);
  }, [data]);

  const totalPages = Math.max(1, Math.ceil(data.length / pageSize));
  const pagedData = data.slice((page - 1) * pageSize, page * pageSize);

  const renderSkeleton = () => {
    return Array.from({ length: pageSize / 2 }).map((_, index) => (
      <tr key={`skeleton-${index}`}>
        <td colSpan={columns.length} className="px-8 py-6">
          <div className="h-14 animate-pulse rounded-full bg-[var(--admin-card-strong)]" />
        </td>
      </tr>
    ));
  };

  return (
    <div className="overflow-hidden rounded-[2rem] border border-[var(--admin-border)] bg-[var(--admin-card)]/90 shadow-[var(--admin-shadow)] backdrop-blur-2xl">
      <div className="overflow-x-auto">
        <table className="w-full min-w-[780px] border-collapse">
          <thead>
            <tr className="bg-[var(--admin-card-soft)] text-right">
              {columns.map((col) => (
                <th
                  key={col.key}
                  className={`px-8 py-5 text-sm font-bold uppercase tracking-[0.2em] text-[var(--admin-primary)] text-${col.align || 'right'}`}
                >
                  {col.label}
                </th>
              ))}
            </tr>
          </thead>
          <tbody className="divide-y divide-[var(--admin-border)]">
            {loading ? (
              renderSkeleton()
            ) : pagedData.length === 0 ? (
              <tr>
                <td colSpan={columns.length} className="px-8 py-16 text-center text-[var(--admin-muted)]">
                  {emptyMessage}
                </td>
              </tr>
            ) : (
              pagedData.map((row) => {
                const key = rowKey(row);
                const isExpanded = expandedKeys.has(key);
                return (
                  <React.Fragment key={key}>
                    <tr 
                      className={`transition-colors hover:bg-[var(--admin-hover)] ${(expandedRowRender || onRowClick) ? 'cursor-pointer' : ''}`}
                      onClick={() => {
                        if (onRowClick) {
                          onRowClick(row);
                        } else if (expandedRowRender) {
                          toggleExpand(key);
                        }
                      }}
                    >
                      {columns.map((col) => (
                        <td
                          key={col.key}
                          className={`px-8 py-6 text-sm text-[var(--admin-text)] text-${col.align || 'right'}`}
                        >
                          {col.render(row)}
                        </td>
                      ))}
                    </tr>
                    {isExpanded && expandedRowRender && (
                      <tr className="bg-[var(--admin-card-soft)]">
                        <td colSpan={columns.length} className="px-8 py-4 border-b border-[var(--admin-border)]">
                          {expandedRowRender(row)}
                        </td>
                      </tr>
                    )}
                  </React.Fragment>
                );
              })
            )}
          </tbody>
        </table>
      </div>

      <div className="flex items-center justify-between border-t border-[var(--admin-border)] p-6">
        <span className="text-xs font-bold tracking-[0.18em] text-[var(--admin-muted)]">
          عرض {formatCompactNumber(data.length === 0 ? 0 : (page - 1) * pageSize + 1)}-
          {formatCompactNumber(Math.min(page * pageSize, data.length))} من أصل {formatCompactNumber(data.length)} عنصر
        </span>
        <div className="flex items-center gap-2">
          <button
            disabled={page === 1}
            onClick={() => setPage((p) => p - 1)}
            className="rounded-full p-2 text-[var(--admin-muted)] transition hover:bg-[var(--admin-hover)] hover:text-[var(--admin-text)] disabled:opacity-40"
            aria-label="الصفحة السابقة"
            title="الصفحة السابقة"
          >
            <ChevronRight className="h-5 w-5" />
          </button>
          <span className="px-3 text-sm font-bold text-[var(--admin-primary)]" aria-current="page" aria-label={`الصفحة ${page}`}>{formatCompactNumber(page)}</span>
          <button
            disabled={page >= totalPages}
            onClick={() => setPage((p) => p + 1)}
            className="rounded-full p-2 text-[var(--admin-muted)] transition hover:bg-[var(--admin-hover)] hover:text-[var(--admin-text)] disabled:opacity-40"
            aria-label="الصفحة التالية"
            title="الصفحة التالية"
          >
            <ChevronLeft className="h-5 w-5" />
          </button>
        </div>
      </div>
    </div>
  );
}
