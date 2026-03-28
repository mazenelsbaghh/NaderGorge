"use client";

import * as React from "react";
import { cn } from "@/lib/utils";

// --- Context & Types ---

interface CheckboxContextValue {
  id?: string;
  name?: string;
  value?: string;
  isSelected: boolean;
  isIndeterminate: boolean;
  isDisabled: boolean;
  isInvalid: boolean;
  onChange: (e: React.ChangeEvent<HTMLInputElement>) => void;
}

const CheckboxContext = React.createContext<CheckboxContextValue | null>(null);

export const useCheckboxContext = () => {
  const context = React.useContext(CheckboxContext);
  if (!context) {
    throw new Error("Checkbox subcomponents must be used within a <Checkbox> wrapper.");
  }
  return context;
};

export interface CheckboxProps extends Omit<React.InputHTMLAttributes<HTMLInputElement>, 'onChange' | 'value' | 'children'> {
  id?: string;
  name?: string;
  value?: string;
  defaultSelected?: boolean;
  isSelected?: boolean;
  isIndeterminate?: boolean;
  isDisabled?: boolean;
  isInvalid?: boolean;
  onChange?: (selected: boolean) => void;
  children?: React.ReactNode | ((props: any) => React.ReactNode);
  className?: string;
  render?: (props: any) => React.ReactNode;
}

// --- Root Component ---

/**
 * Headless-friendly Checkbox implementation fully compatible with HeroUI structure.
 * Wraps children with context providing selected state matching the BEM CSS schema.
 */
const CheckboxRoot = React.forwardRef<HTMLInputElement, CheckboxProps>((props, ref) => {
  const {
    id,
    name,
    value,
    defaultSelected = false,
    isSelected: controlledSelected,
    isIndeterminate = false,
    isDisabled = false,
    isInvalid = false,
    onChange,
    children,
    className,
    render,
    ...rest
  } = props;

  const [internalSelected, setInternalSelected] = React.useState(defaultSelected);
  const isSelected = controlledSelected !== undefined ? controlledSelected : internalSelected;

  const handleChange = (e: React.ChangeEvent<HTMLInputElement>) => {
    if (isDisabled) return;
    const newChecked = e.target.checked;
    setInternalSelected(newChecked);
    if (onChange) onChange(newChecked);
  };

  const contextValue: CheckboxContextValue = {
    id,
    name,
    value,
    isSelected,
    isIndeterminate,
    isDisabled,
    isInvalid,
    onChange: handleChange,
  };

  const wrapperProps = {
    className: cn("group relative flex items-start gap-3 cursor-pointer select-none outline-none", isDisabled && "opacity-60 cursor-not-allowed", className),
    "data-selected": isSelected,
    "data-disabled": isDisabled,
    "data-invalid": isInvalid,
    "data-indeterminate": isIndeterminate,
  };

  if (render) {
    return (
      <CheckboxContext.Provider value={contextValue}>
        {render({ ...wrapperProps, isSelected, isIndeterminate, isDisabled })}
      </CheckboxContext.Provider>
    );
  }

  return (
    <CheckboxContext.Provider value={contextValue}>
      <label {...wrapperProps} htmlFor={id}>
        <input
          type="checkbox"
          ref={ref}
          id={id}
          name={name}
          value={value}
          disabled={isDisabled}
          checked={isSelected}
          onChange={handleChange}
          className="sr-only"
          {...rest}
        />
        {typeof children === "function" ? children({ isSelected, isIndeterminate, isDisabled }) : children}
      </label>
    </CheckboxContext.Provider>
  );
});
CheckboxRoot.displayName = "Checkbox";

// --- Compound Components ---

const CheckboxControl = React.forwardRef<HTMLDivElement, React.HTMLAttributes<HTMLDivElement>>(({ className, children, ...props }, ref) => {
  const { isSelected, isDisabled, isInvalid, isIndeterminate } = useCheckboxContext();

  return (
    <div
      ref={ref}
      aria-hidden="true"
      className={cn(
        "relative flex h-6 w-6 shrink-0 items-center justify-center rounded-lg border-2 transition-all duration-300 ease-out overflow-hidden",
        isSelected || isIndeterminate
          ? "border-[var(--admin-primary)] bg-[var(--admin-primary)] shadow-[0_4px_12px_var(--admin-primary-15)] scale-100"
          : "border-[var(--admin-border)] bg-[var(--admin-card)] group-hover:border-[var(--admin-primary)]/60 scale-95 group-hover:scale-100",
        isInvalid && "border-rose-500",
        className
      )}
      data-selected={isSelected}
      data-disabled={isDisabled}
      data-invalid={isInvalid}
      data-indeterminate={isIndeterminate}
      {...props}
    >
      {children}
    </div>
  );
});
CheckboxControl.displayName = "Checkbox.Control";

const CheckboxIndicator = React.forwardRef<
  HTMLDivElement,
  { className?: string; children?: React.ReactNode | ((props: any) => React.ReactNode) }
>((props, ref) => {
  const { isSelected, isIndeterminate } = useCheckboxContext();
  const { className, children, ...rest } = props;

  return (
    <div
      ref={ref}
      className={cn(
        "flex h-full w-full items-center justify-center text-white transition-all duration-300 ease-out",
        isSelected || isIndeterminate ? "opacity-100 scale-100" : "opacity-0 scale-50",
        className
      )}
      data-selected={isSelected || isIndeterminate}
      {...rest}
    >
      {typeof children === "function"
        ? children({ isSelected, isIndeterminate })
        : children
          ? children
          : isIndeterminate ? (
            <svg stroke="currentColor" strokeWidth={3} viewBox="0 0 24 24" className="w-full h-full text-white">
              <line x1="21" x2="3" y1="12" y2="12" />
            </svg>
          ) : (
            <svg
              fill="none"
              viewBox="0 0 24 24"
              stroke="currentColor"
              strokeWidth="3.5"
              strokeLinecap="round"
              strokeLinejoin="round"
              className="w-[85%] h-[85%] text-white"
            >
              <polyline points="20 6 9 17 4 12"></polyline>
            </svg>
          )}
    </div>
  );
});
CheckboxIndicator.displayName = "Checkbox.Indicator";

const CheckboxContent = React.forwardRef<HTMLDivElement, React.HTMLAttributes<HTMLDivElement>>(({ className, ...props }, ref) => {
  return <div ref={ref} className={cn("flex flex-col gap-0.5 pt-0.5", className)} {...props} />;
});
CheckboxContent.displayName = "Checkbox.Content";

// --- Export Utility Form Components (Mirrors HeroUI) ---

export const Label = React.forwardRef<HTMLLabelElement, React.LabelHTMLAttributes<HTMLLabelElement>>(({ className, ...props }, ref) => {
  // Uses span instead of label inside the checkbox to avoid nested label bugs since CheckboxRoot is already a label
  return <span ref={ref as any} className={cn("text-sm font-bold text-[var(--admin-text)] transition-colors group-hover:text-[var(--admin-primary)]", className)} {...(props as any)} />;
});
Label.displayName = "Label";

export const Description = React.forwardRef<HTMLParagraphElement, React.HTMLAttributes<HTMLParagraphElement>>(({ className, ...props }, ref) => {
  return <p ref={ref} className={cn("text-xs text-[var(--admin-muted)] select-none", className)} {...props} />;
});
Description.displayName = "Description";

// Attach subcomponents to Root
export const Checkbox = Object.assign(CheckboxRoot, {
  Control: CheckboxControl,
  Indicator: CheckboxIndicator,
  Content: CheckboxContent,
});
