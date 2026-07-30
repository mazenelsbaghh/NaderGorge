'use client';

import * as React from 'react';

interface RadioContextValue {
  name: string;
  value: string;
  onChange: (value: string) => void;
  orientation: 'horizontal' | 'vertical';
}

const RadioGroupContext = React.createContext<RadioContextValue | null>(null);
const RadioItemContext = React.createContext<{ isSelected: boolean } | null>(null);

export interface RadioGroupProps {
  name: string;
  value?: string;
  defaultValue?: string;
  onChange?: (value: string) => void;
  orientation?: 'horizontal' | 'vertical';
  className?: string;
  children: React.ReactNode;
}

export function RadioGroup({
  name,
  value,
  defaultValue,
  onChange,
  orientation = 'vertical',
  className,
  children,
}: RadioGroupProps) {
  const [internalValue, setInternalValue] = React.useState(value ?? defaultValue ?? '');

  React.useEffect(() => {
    if (value !== undefined) {
      setInternalValue(value);
    }
  }, [value]);

  const handleChange = (newValue: string) => {
    if (value === undefined) {
      setInternalValue(newValue);
    }
    onChange?.(newValue);
  };

  return (
    <RadioGroupContext.Provider
      value={{ name, value: internalValue, onChange: handleChange, orientation }}
    >
      <div
        className={`flex ${orientation === 'horizontal' ? 'flex-row gap-2' : 'flex-col gap-3'} ${className || ''}`}
        role="radiogroup"
      >
        {children}
      </div>
    </RadioGroupContext.Provider>
  );
}

export interface RadioProps {
  value: string;
  className?: string;
  children: React.ReactNode | ((props: { isSelected: boolean }) => React.ReactNode);
}

export function Radio({ value, className, children }: RadioProps) {
  const context = React.useContext(RadioGroupContext);
  if (!context) {
    throw new Error('Radio must be used within a RadioGroup');
  }

  const isSelected = context.value === value;

  return (
    <RadioItemContext.Provider value={{ isSelected }}>
      <label
        className={`group flex flex-1 cursor-pointer items-center gap-2.5 rounded-[18px] border-2 p-3 transition-all ${
          isSelected
            ? 'border-[var(--admin-primary)] bg-[var(--admin-primary)]/5'
            : 'border-[var(--admin-border)] bg-[var(--admin-card-soft)] hover:border-[var(--admin-text)]/20'
        } ${className || ''}`}
      >
        <input
          type="radio"
          className="sr-only"
          name={context.name}
          value={value}
          checked={isSelected}
          onChange={() => context.onChange(value)}
        />
        {typeof children === 'function' ? children({ isSelected }) : children}
      </label>
    </RadioItemContext.Provider>
  );
}

// Indicator subcomponent
Radio.Indicator = function RadioIndicator({ className }: { className?: string }) {
  const context = React.useContext(RadioItemContext);
  const isSelected = context?.isSelected;

  return (
    <div
      className={`flex h-4 w-4 shrink-0 items-center justify-center rounded-full border-2 transition-colors ${
        isSelected
          ? 'border-[var(--admin-primary)]'
          : 'border-[var(--admin-muted)] group-hover:border-[var(--admin-text)]/40'
      } ${className || ''}`}
    >
      {isSelected && <div className="h-2 w-2 rounded-full bg-[var(--admin-primary)] shadow-sm" />}
    </div>
  );
};

// Content subcomponent
Radio.Content = function RadioContent({
  children,
  className,
}: {
  children: React.ReactNode;
  className?: string;
}) {
  const context = React.useContext(RadioItemContext);
  const isSelected = context?.isSelected;

  return (
    <span
      className={`text-sm font-bold transition-colors w-full ${
        isSelected ? 'text-[var(--admin-primary)]' : 'text-[var(--admin-text)]'
      } ${className || ''}`}
    >
      {children}
    </span>
  );
};
