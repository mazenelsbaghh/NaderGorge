'use client';

import React, { useState, useEffect } from 'react';
import { ChevronDown, ChevronLeft, ChevronRight, ExternalLink } from 'lucide-react';
import { formatCompactNumber } from './admin-utils';

export interface AdminColumn<T> {
  key: string;
  label: React.ReactNode;
  render: (row: T) => React.ReactNode;
  align?: 'right' | 'left' | 'center';
}

const alignmentClasses = {
  right: 'text-right',
  left: 'text-left',
  center: 'text-center',
} as const;

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
  pagination?: boolean;
  errorMessage?: string | null;
  onRetry?: () => void;
  expandedRowRender?: (record: T) => React.ReactNode;
  onRowClick?: (record: T) => void;
  rowActionLabel?: (record: T) => string;
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
  pagination = true,
  emptyMessage = 'لا توجد نتائج.',
  errorMessage,
  onRetry,
  rowKey,
  expandedRowRender,
  onRowClick,
  rowActionLabel,
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
  const displayedData = pagination ? data.slice((page - 1) * pageSize, page * pageSize) : data;

  const activateRow = (row: T, key: string | number) => {
    if (onRowClick) {
      onRowClick(row);
    } else if (expandedRowRender) {
      toggleExpand(key);
    }
  };

  const hasRowAction = Boolean(expandedRowRender || onRowClick);
  const totalColumns = columns.length + (hasRowAction ? 1 : 0);

  const renderSkeleton = () => {
    return Array.from({ length: pageSize / 2 }).map((_, index) => (
      <tr key={`skeleton-${index}`}>
        <td colSpan={totalColumns} className="px-8 py-6">
          <div className="h-14 animate-pulse rounded-full bg-[var(--admin-card-strong)]" />
        </td>
      </tr>
    ));
  };

  return (
    <div className="overflow-hidden rounded-2xl border border-[var(--admin-border)] bg-[var(--admin-card)] shadow-[var(--admin-shadow)]">
      <div className="overflow-x-auto">
        <table className="w-full min-w-[780px] border-collapse">
          <thead>
            <tr className="bg-[var(--admin-card-soft)] text-right">
              {columns.map((col) => (
                <th
                  key={col.key}
                  scope="col"
                  className={`px-8 py-5 text-sm font-bold text-[var(--admin-primary)] ${alignmentClasses[col.align ?? 'right']}`}
                >
                  {col.label}
                </th>
              ))}
              {hasRowAction && <th scope="col" className="w-24 px-4 py-5 text-center text-sm font-bold text-[var(--admin-primary)]">الإجراء</th>}
            </tr>
          </thead>
          <tbody className="divide-y divide-[var(--admin-border)]">
            {loading ? (
              renderSkeleton()
            ) : errorMessage ? (
              <tr>
                <td colSpan={totalColumns} className="px-8 py-14 text-center">
                  <div role="alert" className="mx-auto flex max-w-lg flex-col items-center gap-3">
                    <p className="text-sm font-bold text-[var(--admin-danger)]">{errorMessage}</p>
                    {onRetry && (
                      <button
                        type="button"
                        onClick={onRetry}
                        className="rounded-full border border-[var(--admin-border)] bg-[var(--admin-card-soft)] px-5 py-2 text-xs font-black text-[var(--admin-text)] transition hover:bg-[var(--admin-hover)] focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-[var(--admin-primary)]"
                      >
                        إعادة المحاولة
                      </button>
                    )}
                  </div>
                </td>
              </tr>
            ) : displayedData.length === 0 ? (
              <tr>
                <td colSpan={totalColumns} className="px-8 py-16 text-center text-[var(--admin-muted)]">
                  {emptyMessage}
                </td>
              </tr>
            ) : (
              displayedData.map((row) => {
                const key = rowKey(row);
                const isExpanded = expandedKeys.has(key);
                const expandedRowId = `admin-table-expanded-${String(key)}`;
                return (
                  <React.Fragment key={key}>
                    <tr className="transition-colors hover:bg-[var(--admin-hover)]">
                      {columns.map((col) => (
                        <td
                          key={col.key}
                          className={`px-8 py-6 text-sm text-[var(--admin-text)] ${alignmentClasses[col.align ?? 'right']}`}
                        >
                          {col.render(row)}
                        </td>
                      ))}
                      {hasRowAction && (
                        <td className="px-4 py-4 text-center">
                          <button
                            type="button"
                            onClick={() => activateRow(row, key)}
                            aria-expanded={expandedRowRender ? isExpanded : undefined}
                            aria-controls={expandedRowRender ? expandedRowId : undefined}
                            aria-label={rowActionLabel?.(row) ?? 'عرض تفاصيل الصف'}
                            className="inline-flex min-h-11 min-w-11 items-center justify-center rounded-xl border border-[var(--admin-border)] bg-[var(--admin-card-soft)] text-[var(--admin-primary)] transition hover:bg-[var(--admin-hover)]"
                          >
                            {expandedRowRender ? (
                              <ChevronDown className={`h-5 w-5 transition-transform ${isExpanded ? 'rotate-180' : ''}`} aria-hidden="true" />
                            ) : (
                              <ExternalLink className="h-5 w-5" aria-hidden="true" />
                            )}
                          </button>
                        </td>
                      )}
                    </tr>
                    {isExpanded && expandedRowRender && (
                      <tr className="bg-[var(--admin-card-soft)]">
                        <td id={expandedRowId} colSpan={totalColumns} className="border-b border-[var(--admin-border)] px-8 py-4">
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

      {pagination && (
        <div className="flex items-center justify-between border-t border-[var(--admin-border)] p-6">
          <span className="text-xs font-bold tracking-[0.18em] text-[var(--admin-muted)]">
            عرض {formatCompactNumber(data.length === 0 ? 0 : (page - 1) * pageSize + 1)}-
            {formatCompactNumber(Math.min(page * pageSize, data.length))} من أصل {formatCompactNumber(data.length)} عنصر
          </span>
          <div className="flex items-center gap-2">
            <button
              type="button"
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
              type="button"
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
      )}
    </div>
  );
}
