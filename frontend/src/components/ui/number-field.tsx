"use client";

import * as React from "react";
import { cn } from "@/lib/utils";
import { Minus, Plus } from "lucide-react";

interface NumberFieldContextValue {
  value: number;
  setValue: (value: number) => void;
  minValue?: number;
  maxValue?: number;
  step: number;
  isDisabled: boolean;
  isInvalid: boolean;
  id?: string;
  name?: string;
}

const NumberFieldContext = React.createContext<NumberFieldContextValue | null>(null);

export const useNumberFieldContext = () => {
  const context = React.useContext(NumberFieldContext);
  if (!context) {
    throw new Error("NumberField subcomponents must be used within a <NumberField> wrapper.");
  }
  return context;
};

export interface NumberFieldProps extends Omit<React.HTMLAttributes<HTMLDivElement>, 'onChange'> {
  value?: number;
  defaultValue?: number;
  onChange?: (value: number) => void;
  minValue?: number;
  maxValue?: number;
  step?: number;
  isDisabled?: boolean;
  isInvalid?: boolean;
  id?: string;
  name?: string;
}

const NumberFieldRoot = React.forwardRef<HTMLDivElement, NumberFieldProps>((props, ref) => {
  const {
    value: controlledValue,
    defaultValue = 0,
    onChange,
    minValue,
    maxValue,
    step = 1,
    isDisabled = false,
    isInvalid = false,
    id,
    name,
    className,
    children,
    ...rest
  } = props;

  const [internalValue, setInternalValue] = React.useState(controlledValue ?? defaultValue);
  
  React.useEffect(() => {
    if (controlledValue === undefined) {
      setInternalValue(defaultValue);
    } else {
      setInternalValue(controlledValue);
    }
  }, [controlledValue, defaultValue]);

  const value = controlledValue !== undefined ? controlledValue : internalValue;

  const setValue = React.useCallback(
    (newValue: number) => {
      let clamped = newValue;
      if (minValue !== undefined) clamped = Math.max(minValue, clamped);
      if (maxValue !== undefined) clamped = Math.min(maxValue, clamped);
      
      if (controlledValue === undefined) {
        setInternalValue(clamped);
      }
      if (onChange) {
        onChange(clamped);
      }
    },
    [controlledValue, minValue, maxValue, onChange]
  );

  const contextValue: NumberFieldContextValue = {
    value,
    setValue,
    minValue,
    maxValue,
    step,
    isDisabled,
    isInvalid,
    id,
    name,
  };

  return (
    <NumberFieldContext.Provider value={contextValue}>
      <div
        ref={ref}
        className={cn("flex flex-col gap-1 w-full", className)}
        data-disabled={isDisabled}
        data-invalid={isInvalid}
        {...rest}
      >
        {children}
      </div>
    </NumberFieldContext.Provider>
  );
});
NumberFieldRoot.displayName = "NumberField";

const NumberFieldGroup = React.forwardRef<HTMLDivElement, React.HTMLAttributes<HTMLDivElement>>(({ className, children, ...props }, ref) => {
  const { isDisabled, isInvalid } = useNumberFieldContext();

  return (
    <div
      ref={ref}
      className={cn(
        "group relative flex items-center overflow-hidden rounded-xl border bg-[var(--admin-card-soft)] transition-all duration-300",
        isDisabled ? "opacity-60 cursor-not-allowed" : "hover:shadow-sm focus-within:border-[var(--admin-primary)] focus-within:ring-1 focus-within:ring-[var(--admin-primary)]",
        isInvalid ? "border-rose-500" : "border-[var(--admin-border)]",
        className
      )}
      dir="ltr"
      {...props}
    >
      {children}
    </div>
  );
});
NumberFieldGroup.displayName = "NumberField.Group";

const NumberFieldInput = React.forwardRef<HTMLInputElement, Omit<React.InputHTMLAttributes<HTMLInputElement>, 'value' | 'onChange'>>(({ className, ...props }, ref) => {
  const { value, setValue, isDisabled, id, name, minValue, maxValue } = useNumberFieldContext();

  const handleChange = (e: React.ChangeEvent<HTMLInputElement>) => {
    const val = parseFloat(e.target.value);
    if (!isNaN(val)) {
      setValue(val);
    }
  };

  const handleBlur = (e: React.FocusEvent<HTMLInputElement>) => {
    let val = parseFloat(e.target.value);
    if (isNaN(val)) val = minValue !== undefined ? minValue : 0;
    setValue(val);
  };

  return (
    <input
      ref={ref}
      type="number"
      id={id}
      name={name}
      value={value}
      onChange={handleChange}
      onBlur={handleBlur}
      disabled={isDisabled}
      min={minValue}
      max={maxValue}
      className={cn(
        "w-full min-w-0 flex-1 appearance-none bg-transparent px-3 py-2 text-center text-sm font-bold text-[var(--admin-text)] outline-none tabular-nums [&::-webkit-inner-spin-button]:appearance-none [&::-webkit-outer-spin-button]:appearance-none",
        className
      )}
      dir="ltr"
      {...props}
    />
  );
});
NumberFieldInput.displayName = "NumberField.Input";

const NumberFieldIncrementButton = React.forwardRef<HTMLButtonElement, React.ButtonHTMLAttributes<HTMLButtonElement>>(({ className, children, ...props }, ref) => {
  const { value, setValue, step, maxValue, isDisabled: fieldDisabled } = useNumberFieldContext();
  
  const isMaxReached = maxValue !== undefined && value >= maxValue;
  const disabled = fieldDisabled || isMaxReached;

  return (
    <button
      ref={ref}
      type="button"
      disabled={disabled}
      onClick={() => setValue(value + step)}
      className={cn(
        "flex h-full min-h-[44px] w-12 shrink-0 items-center justify-center border-l bg-[var(--admin-card)] border-[var(--admin-border)] text-[var(--admin-text)] transition-colors",
        disabled ? "opacity-50 cursor-not-allowed" : "hover:bg-[var(--admin-card-strong)] hover:text-[var(--admin-primary)] active:bg-[var(--admin-border)]",
        className
      )}
      {...props}
    >
      {children || <Plus className="h-5 w-5 shrink-0" />}
    </button>
  );
});
NumberFieldIncrementButton.displayName = "NumberField.IncrementButton";

const NumberFieldDecrementButton = React.forwardRef<HTMLButtonElement, React.ButtonHTMLAttributes<HTMLButtonElement>>(({ className, children, ...props }, ref) => {
  const { value, setValue, step, minValue, isDisabled: fieldDisabled } = useNumberFieldContext();
  
  const isMinReached = minValue !== undefined && value <= minValue;
  const disabled = fieldDisabled || isMinReached;

  return (
    <button
      ref={ref}
      type="button"
      disabled={disabled}
      onClick={() => setValue(value - step)}
      className={cn(
        "flex h-full min-h-[44px] w-12 shrink-0 items-center justify-center border-r bg-[var(--admin-card)] border-[var(--admin-border)] text-[var(--admin-text)] transition-colors",
        disabled ? "opacity-50 cursor-not-allowed" : "hover:bg-[var(--admin-card-strong)] hover:text-[var(--admin-primary)] active:bg-[var(--admin-border)]",
        className
      )}
      {...props}
    >
      {children || <Minus className="h-5 w-5 shrink-0" />}
    </button>
  );
});
NumberFieldDecrementButton.displayName = "NumberField.DecrementButton";

export const Label = React.forwardRef<HTMLLabelElement, React.LabelHTMLAttributes<HTMLLabelElement>>(({ className, ...props }, ref) => {
  return <label ref={ref} className={cn("text-sm font-bold text-[var(--admin-muted)] block select-none", className)} {...props} />;
});
Label.displayName = "Label";

export const Description = React.forwardRef<HTMLParagraphElement, React.HTMLAttributes<HTMLParagraphElement>>(({ className, ...props }, ref) => {
  return <p ref={ref} className={cn("text-xs text-[var(--admin-muted)] select-none", className)} {...props} />;
});
Description.displayName = "Description";

export const FieldError = React.forwardRef<HTMLParagraphElement, React.HTMLAttributes<HTMLParagraphElement>>(({ className, ...props }, ref) => {
  const { isInvalid } = useNumberFieldContext();
  if (!isInvalid) return null;
  return <p ref={ref} className={cn("text-xs font-bold text-rose-500", className)} {...props} />;
});
FieldError.displayName = "FieldError";

export const NumberField = Object.assign(NumberFieldRoot, {
  Group: NumberFieldGroup,
  Input: NumberFieldInput,
  IncrementButton: NumberFieldIncrementButton,
  DecrementButton: NumberFieldDecrementButton,
  Label: Label,
  Description: Description,
  FieldError: FieldError,
});
