"use client";

import * as React from "react";
import { cn } from "@/lib/utils";

type StepperContextValue = {
  value: number;
  setValue: (value: number) => void;
  indicators?: {
    completed?: React.ReactNode;
    loading?: React.ReactNode;
  };
};

type StepperItemContextValue = {
  step: number;
  state: "completed" | "active" | "inactive";
};

const StepperContext = React.createContext<StepperContextValue | null>(null);
const StepperItemContext = React.createContext<StepperItemContextValue | null>(null);

function useStepper() {
  const context = React.useContext(StepperContext);
  if (!context) {
    throw new Error("Stepper components must be used inside Stepper");
  }
  return context;
}

function useStepperItem() {
  const context = React.useContext(StepperItemContext);
  if (!context) {
    throw new Error("Stepper item components must be used inside StepperItem");
  }
  return context;
}

export type StepperProps = React.HTMLAttributes<HTMLDivElement> & {
  defaultValue?: number;
  value?: number;
  onValueChange?: (value: number) => void;
  indicators?: StepperContextValue["indicators"];
};

export function Stepper({
  defaultValue = 1,
  value,
  onValueChange,
  indicators,
  className,
  children,
  ...props
}: StepperProps) {
  const [internalValue, setInternalValue] = React.useState(defaultValue);
  const currentValue = value ?? internalValue;

  const setValue = React.useCallback(
    (nextValue: number) => {
      if (value === undefined) {
        setInternalValue(nextValue);
      }
      onValueChange?.(nextValue);
    },
    [onValueChange, value]
  );

  return (
    <StepperContext.Provider value={{ value: currentValue, setValue, indicators }}>
      <div className={cn("w-full", className)} {...props}>
        {children}
      </div>
    </StepperContext.Provider>
  );
}

export function StepperNav({ className, ...props }: React.HTMLAttributes<HTMLDivElement>) {
  return (
    <div
      role="tablist"
      className={cn("flex w-full items-stretch overflow-x-auto", className)}
      {...props}
    />
  );
}

export type StepperItemProps = React.HTMLAttributes<HTMLDivElement> & {
  step: number;
};

export function StepperItem({ step, className, children, ...props }: StepperItemProps) {
  const { value } = useStepper();
  const state = step < value ? "completed" : step === value ? "active" : "inactive";

  return (
    <StepperItemContext.Provider value={{ step, state }}>
      <div
        data-state={state}
        className={cn("group/step", className)}
        {...props}
      >
        {children}
      </div>
    </StepperItemContext.Provider>
  );
}

export function StepperTrigger({
  className,
  children,
  ...props
}: React.ButtonHTMLAttributes<HTMLButtonElement>) {
  const { setValue } = useStepper();
  const { step, state } = useStepperItem();

  return (
    <button
      type="button"
      role="tab"
      aria-selected={state === "active"}
      data-state={state}
      onClick={() => setValue(step)}
      className={cn(
        "group/step-trigger rounded-xl text-start outline-none transition focus-visible:ring-2 focus-visible:ring-[var(--student-primary)] focus-visible:ring-offset-2 focus-visible:ring-offset-[var(--student-bg)]",
        className
      )}
      {...props}
    >
      {children}
    </button>
  );
}

export function StepperIndicator({
  className,
  children,
  ...props
}: React.HTMLAttributes<HTMLSpanElement>) {
  const { indicators } = useStepper();
  const { state } = useStepperItem();

  return (
    <span
      data-state={state}
      className={cn(
        "grid h-9 w-9 shrink-0 place-items-center rounded-xl border text-sm font-black transition",
        state === "completed" && "border-[#0E8F8F] bg-[#0E8F8F] text-white",
        state === "active" && "border-[#0A1D3D] bg-[#0A1D3D] text-white shadow-sm",
        state === "inactive" && "border-[var(--student-border)] bg-[var(--student-card-soft)] text-[var(--student-muted)]",
        className
      )}
      {...props}
    >
      {state === "completed" && indicators?.completed ? indicators.completed : children}
    </span>
  );
}

export function StepperTitle({ className, ...props }: React.HTMLAttributes<HTMLSpanElement>) {
  return (
    <span
      className={cn(
        "block text-sm font-black text-[var(--student-text)] transition",
        "group-data-[state=inactive]/step:text-[var(--student-muted)]",
        className
      )}
      {...props}
    />
  );
}

export function StepperPanel({ className, ...props }: React.HTMLAttributes<HTMLDivElement>) {
  return <div className={cn("min-w-0", className)} {...props} />;
}

export type StepperContentProps = React.HTMLAttributes<HTMLDivElement> & {
  value: number;
};

export function StepperContent({ value, className, ...props }: StepperContentProps) {
  const { value: activeValue } = useStepper();

  if (value !== activeValue) {
    return null;
  }

  return (
    <div
      role="tabpanel"
      className={cn("animate-in fade-in-0 slide-in-from-bottom-2 duration-200", className)}
      {...props}
    />
  );
}
