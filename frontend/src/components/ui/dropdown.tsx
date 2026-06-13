'use client';

import {
  useCallback,
  useEffect,
  useId,
  useMemo,
  useRef,
  useState,
  type ReactNode,
} from 'react';
import { ChevronDown, Check, Search } from 'lucide-react';

/* ── Types ────────────────────────────────────────────────── */

export interface DropdownOption {
  /** Unique value used in onChange */
  value: string;
  /** Displayed label */
  label: string;
  /** Optional icon rendered before label */
  icon?: ReactNode;
  /** Group heading this option belongs to */
  group?: string;
  /** Whether this option is disabled */
  disabled?: boolean;
}

export interface DropdownProps {
  /** Currently selected value(s) */
  value: string | string[];
  /** Called when selection changes */
  onChange: (value: string | string[]) => void;
  /** List of options */
  options: DropdownOption[];
  /** Placeholder when nothing selected */
  placeholder?: string;
  /** Optional label above the dropdown */
  label?: string;
  /** Disabled state */
  disabled?: boolean;
  /** Enable multi-select */
  multiple?: boolean;
  /** Enable search/filter */
  searchable?: boolean;
  /** Search placeholder */
  searchPlaceholder?: string;
  /** Custom className on the wrapper */
  className?: string;
  /** Error message */
  error?: string;
  /** HTML name attribute for form submission */
  name?: string;
  /** Size variant */
  size?: 'sm' | 'md' | 'lg';
}

/* ── Component ───────────────────────────────────────────── */

export function Dropdown({
  value,
  onChange,
  options,
  placeholder = 'اختر...',
  label,
  disabled = false,
  multiple = false,
  searchable = false,
  searchPlaceholder = 'ابحث...',
  className = '',
  error,
  name,
  size = 'md',
}: DropdownProps) {
  const uid = useId();
  const [isOpen, setIsOpen] = useState(false);
  const [search, setSearch] = useState('');
  const [highlightedIdx, setHighlightedIdx] = useState(-1);
  const wrapperRef = useRef<HTMLDivElement>(null);
  const listRef = useRef<HTMLUListElement>(null);
  const searchRef = useRef<HTMLInputElement>(null);
  const triggerRef = useRef<HTMLButtonElement>(null);

  /* Normalise value to array for unified logic */
  const selected = useMemo(
    () => (Array.isArray(value) ? value : value ? [value] : []),
    [value],
  );

  /* ── Filtered options ─────────────────────────────────── */
  const filtered = search.trim()
    ? options.filter((o) =>
        o.label.toLowerCase().includes(search.trim().toLowerCase()),
      )
    : options;

  /* ── Group support ────────────────────────────────────── */
  const hasGroups = options.some((o) => o.group);
  const groups = hasGroups
    ? Array.from(new Set(options.map((o) => o.group).filter(Boolean)))
    : [];

  /* ── Display text ─────────────────────────────────────── */
  const displayText = (() => {
    if (selected.length === 0) return null;
    if (multiple && selected.length > 1) {
      return `${selected.length} عنصر محدد`;
    }
    const opt = options.find((o) => o.value === selected[0]);
    return opt?.label ?? selected[0];
  })();

  const displayIcon = (() => {
    if (selected.length !== 1) return null;
    return options.find((o) => o.value === selected[0])?.icon ?? null;
  })();

  /* ── Toggle / Select ──────────────────────────────────── */
  const toggle = useCallback(() => {
    if (disabled) return;
    setIsOpen((prev) => {
      if (!prev) {
        setSearch('');
        setHighlightedIdx(-1);
      }
      return !prev;
    });
  }, [disabled]);

  const selectOption = useCallback(
    (opt: DropdownOption) => {
      if (opt.disabled) return;
      if (multiple) {
        const next = selected.includes(opt.value)
          ? selected.filter((v) => v !== opt.value)
          : [...selected, opt.value];
        onChange(next);
      } else {
        onChange(opt.value);
        setIsOpen(false);
      }
    },
    [multiple, onChange, selected],
  );

  /* ── Click outside ────────────────────────────────────── */
  useEffect(() => {
    if (!isOpen) return;
    function handler(e: MouseEvent) {
      if (
        wrapperRef.current &&
        !wrapperRef.current.contains(e.target as Node)
      ) {
        setIsOpen(false);
      }
    }
    document.addEventListener('mousedown', handler);
    return () => document.removeEventListener('mousedown', handler);
  }, [isOpen]);

  /* ── Focus search on open ─────────────────────────────── */
  useEffect(() => {
    if (isOpen && searchable) {
      requestAnimationFrame(() => searchRef.current?.focus());
    }
  }, [isOpen, searchable]);

  /* ── Keyboard nav ─────────────────────────────────────── */
  const handleKeyDown = useCallback(
    (e: React.KeyboardEvent) => {
      if (disabled) return;

      switch (e.key) {
        case 'Enter':
        case ' ': {
          e.preventDefault();
          if (!isOpen) {
            setIsOpen(true);
            return;
          }
          if (highlightedIdx >= 0 && highlightedIdx < filtered.length) {
            selectOption(filtered[highlightedIdx]);
          }
          break;
        }
        case 'ArrowDown': {
          e.preventDefault();
          if (!isOpen) {
            setIsOpen(true);
            return;
          }
          setHighlightedIdx((prev) => {
            let next = prev + 1;
            while (next < filtered.length && filtered[next].disabled) next++;
            return next < filtered.length ? next : prev;
          });
          break;
        }
        case 'ArrowUp': {
          e.preventDefault();
          setHighlightedIdx((prev) => {
            let next = prev - 1;
            while (next >= 0 && filtered[next].disabled) next--;
            return next >= 0 ? next : prev;
          });
          break;
        }
        case 'Escape': {
          e.preventDefault();
          setIsOpen(false);
          triggerRef.current?.focus();
          break;
        }
        case 'Home': {
          e.preventDefault();
          setHighlightedIdx(0);
          break;
        }
        case 'End': {
          e.preventDefault();
          setHighlightedIdx(filtered.length - 1);
          break;
        }
      }
    },
    [disabled, isOpen, highlightedIdx, filtered, selectOption],
  );

  /* ── Scroll highlighted into view ─────────────────────── */
  useEffect(() => {
    if (highlightedIdx < 0 || !listRef.current) return;
    const el = listRef.current.children[highlightedIdx] as HTMLElement;
    el?.scrollIntoView({ block: 'nearest' });
  }, [highlightedIdx]);

  /* ── Size classes ──────────────────────────────────────── */
  const sizeMap = {
    sm: 'min-h-[36px] px-3 py-1.5 text-xs rounded-xl',
    md: 'min-h-[44px] px-4 py-2.5 text-sm rounded-2xl',
    lg: 'min-h-[52px] px-5 py-3 text-base rounded-2xl',
  };

  /* ── Render option row ────────────────────────────────── */
  const renderOption = (opt: DropdownOption, idx: number) => {
    const isSelected = selected.includes(opt.value);
    const isHighlighted = highlightedIdx === idx;

    return (
      <li
        key={opt.value}
        id={`${uid}-opt-${idx}`}
        role="option"
        aria-selected={isSelected}
        aria-disabled={opt.disabled}
        className={[
          'group/option relative flex cursor-pointer items-center gap-3 px-4 py-2.5 text-right transition-colors duration-100',
          size === 'sm' ? 'text-xs py-2' : size === 'lg' ? 'text-base py-3' : 'text-sm',
          opt.disabled
            ? 'cursor-not-allowed opacity-40'
            : isHighlighted
              ? 'bg-[var(--admin-primary-15)] text-[var(--admin-primary)]'
              : isSelected
                ? 'bg-[var(--admin-primary-15)]/60 text-[var(--admin-text)]'
                : 'text-[var(--admin-text)] hover:bg-[var(--admin-card-strong)]',
        ].join(' ')}
        onMouseEnter={() => !opt.disabled && setHighlightedIdx(idx)}
        onMouseDown={(e) => {
          e.preventDefault();
          selectOption(opt);
        }}
      >
        {/* Multi-select checkbox */}
        {multiple && (
          <span
            className={[
              'flex h-[18px] w-[18px] shrink-0 items-center justify-center rounded-md border-2 transition-all duration-150',
              isSelected
                ? 'border-[var(--admin-primary)] bg-[var(--admin-primary)] text-[var(--admin-primary-contrast)]'
                : 'border-[var(--admin-border)] bg-[var(--admin-bg)]',
            ].join(' ')}
          >
            {isSelected && <Check className="h-3 w-3" strokeWidth={3} />}
          </span>
        )}

        {/* Icon */}
        {opt.icon && (
          <span className="shrink-0 text-[var(--admin-muted)]">{opt.icon}</span>
        )}

        {/* Label */}
        <span className="flex-1 truncate font-bold">{opt.label}</span>

        {/* Single-select check */}
        {!multiple && isSelected && (
          <Check
            className="h-4 w-4 shrink-0 text-[var(--admin-primary)]"
            strokeWidth={2.5}
          />
        )}
      </li>
    );
  };

  return (
    <div
      ref={wrapperRef}
      className={`relative ${className}`}
      dir="rtl"
      onKeyDown={handleKeyDown}
    >
      {/* Hidden native select for form submission */}
      {name && (
        <select
          name={name}
          multiple={multiple}
          value={multiple ? selected : selected[0] ?? ''}
          onChange={() => {}}
          className="sr-only"
          tabIndex={-1}
          aria-hidden
        >
          {options.map((o) => (
            <option key={o.value} value={o.value}>
              {o.label}
            </option>
          ))}
        </select>
      )}

      {/* Label */}
      {label && (
        <label
          htmlFor={`${uid}-trigger`}
          className="mb-2 block text-sm font-bold text-[var(--admin-text)]"
        >
          {label}
        </label>
      )}

      {/* Trigger button */}
      <button
        ref={triggerRef}
        id={`${uid}-trigger`}
        type="button"
        role="combobox"
        aria-expanded={isOpen}
        aria-haspopup="listbox"
        aria-controls={`${uid}-listbox`}
        aria-label={label || placeholder}
        disabled={disabled}
        onClick={toggle}
        className={[
          'group flex w-full items-center gap-2 border bg-[var(--admin-bg)] font-bold outline-none transition-all duration-200',
          sizeMap[size],
          disabled
            ? 'cursor-not-allowed opacity-50'
            : 'cursor-pointer hover:border-[var(--admin-primary-30)]',
          isOpen
            ? 'border-[var(--admin-primary)] shadow-[0_0_0_3px_var(--admin-primary-15)]'
            : error
              ? 'border-[var(--admin-danger)] shadow-[0_0_0_2px_var(--admin-danger-10)]'
              : 'border-[var(--admin-border)]',
        ].join(' ')}
      >
        {/* Display value */}
        {displayIcon && (
          <span className="shrink-0 text-[var(--admin-muted)]">{displayIcon}</span>
        )}
        <span
          className={`flex-1 truncate text-right ${
            displayText
              ? 'text-[var(--admin-text)]'
              : 'text-[var(--admin-muted)]'
          }`}
        >
          {displayText || placeholder}
        </span>

        {/* Chevron */}
        <ChevronDown
          className={[
            'h-4 w-4 shrink-0 text-[var(--admin-muted)] transition-transform duration-200',
            isOpen ? 'rotate-180' : '',
          ].join(' ')}
        />
      </button>

      {/* Error message */}
      {error && (
        <p className="mt-1.5 text-xs font-bold text-[var(--admin-danger)]">
          {error}
        </p>
      )}

      {/* Dropdown panel */}
      {isOpen && (
        <div
          className={[
            'absolute z-50 mt-2 w-full overflow-hidden rounded-2xl border border-[var(--admin-border)] bg-[var(--admin-card)] shadow-xl',
            /* Entrance animation */
            'animate-in fade-in slide-in-from-top-2 duration-150',
          ].join(' ')}
          style={{
            maxHeight: 'min(320px, 50vh)',
          }}
        >
          {/* Search */}
          {searchable && (
            <div className="sticky top-0 z-10 border-b border-[var(--admin-border)] bg-[var(--admin-card)] p-2">
              <div className="relative">
                <Search className="absolute right-3 top-1/2 h-4 w-4 -translate-y-1/2 text-[var(--admin-muted)]" />
                <input
                  ref={searchRef}
                  type="text"
                  value={search}
                  onChange={(e) => {
                    setSearch(e.target.value);
                    setHighlightedIdx(0);
                  }}
                  placeholder={searchPlaceholder}
                  className="w-full rounded-xl border border-[var(--admin-border)] bg-[var(--admin-bg)] py-2 pr-9 pl-3 text-sm font-medium text-[var(--admin-text)] outline-none transition-all placeholder:text-[var(--admin-muted)] focus:border-[var(--admin-primary)] focus:shadow-[0_0_0_2px_var(--admin-primary-15)]"
                  dir="rtl"
                />
              </div>
            </div>
          )}

          {/* Options list */}
          <ul
            ref={listRef}
            id={`${uid}-listbox`}
            role="listbox"
            aria-multiselectable={multiple}
            className="overflow-y-auto py-1"
            style={{ maxHeight: searchable ? 'calc(min(320px, 50vh) - 56px)' : 'min(320px, 50vh)' }}
          >
            {filtered.length === 0 ? (
              <li className="px-4 py-6 text-center text-sm font-bold text-[var(--admin-muted)]">
                لا توجد نتائج
              </li>
            ) : hasGroups ? (
              groups.map((group) => {
                const groupOptions = filtered.filter((o) => o.group === group);
                if (groupOptions.length === 0) return null;
                return (
                  <li key={group} role="presentation">
                    <div className="sticky top-0 bg-[var(--admin-card-soft)] px-4 py-1.5 text-[10px] font-black uppercase tracking-[0.2em] text-[var(--admin-muted)]">
                      {group}
                    </div>
                    <ul role="group" aria-label={group}>
                      {groupOptions.map((opt) =>
                        renderOption(
                          opt,
                          filtered.indexOf(opt),
                        ),
                      )}
                    </ul>
                  </li>
                );
              })
            ) : (
              filtered.map((opt, idx) => renderOption(opt, idx))
            )}
          </ul>

          {/* Multi footer */}
          {multiple && selected.length > 0 && (
            <div className="border-t border-[var(--admin-border)] bg-[var(--admin-card-soft)] px-4 py-2 text-xs font-bold text-[var(--admin-muted)]">
              {selected.length} محدد
            </div>
          )}
        </div>
      )}
    </div>
  );
}
