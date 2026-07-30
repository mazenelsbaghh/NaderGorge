"use client";

import React, { useState } from "react";
import { motion, AnimatePresence, Variants } from "framer-motion";
import { cn } from "@/lib/utils";
import { Check, ChevronRight, ChevronLeft } from "lucide-react";

export interface StepperStep {
  id: string;
  title?: string;
  status?: 'unvisited' | 'current' | 'answered' | 'skipped';
  content: React.ReactNode;
}

interface AnimatedStepperProps {
  steps: StepperStep[];
  onComplete?: () => void;
  isSubmitting?: boolean;
  activeStep?: number;
  onStepChange?: (step: number) => void;
}

export function AnimatedStepper({ steps, onComplete, isSubmitting, activeStep, onStepChange }: AnimatedStepperProps) {
  const [internalStep, setInternalStep] = useState(0);
  const [direction, setDirection] = useState(1); // 1 = forward, -1 = backward

  const currentStep = activeStep !== undefined ? activeStep : internalStep;

  const handleSetStep = (newStep: number, newDirection: number) => {
    setDirection(newDirection);
    if (activeStep === undefined) {
      setInternalStep(newStep);
    }
    if (onStepChange) {
      onStepChange(newStep);
    }
  };

  const goToNextStep = () => {
    if (currentStep < steps.length - 1) {
      handleSetStep(currentStep + 1, 1);
    } else if (currentStep === steps.length - 1 && onComplete) {
      onComplete();
    }
  };

  const goToPrevStep = () => {
    if (currentStep > 0) {
      handleSetStep(currentStep - 1, -1);
    }
  };

  const variants: Variants = {
    initial: (direction: number) => ({
      x: direction > 0 ? "50%" : "-50%",
      opacity: 0,
    }),
    active: {
      x: 0,
      opacity: 1,
      transition: { type: "spring", stiffness: 300, damping: 30 },
    },
    exit: (direction: number) => ({
      x: direction < 0 ? "50%" : "-50%",
      opacity: 0,
      transition: { type: "spring", stiffness: 300, damping: 30 },
    }),
  };

  if (steps.length === 0) return null;

  return (
    <div className="w-full flex flex-col gap-8 relative z-10">
      {/* Progress Indicators */}
      <div className="flex flex-wrap items-center justify-center gap-3 px-1">
        {steps.map((step, index) => {
           const isCurrent = index === currentStep;
           const s = step.status || (isCurrent ? 'current' : index < currentStep ? 'answered' : 'unvisited');
           let circleClass = "";
           let textClass = "";
           
           if (s === 'current' || isCurrent) {
              circleClass = "border-[3px] border-[var(--admin-primary)] bg-[var(--admin-primary)]/10 shadow-[0_0_15px_rgba(119,90,25,0.2)] scale-110 -translate-y-1 rounded-2xl";
              textClass = "text-[var(--admin-primary)]";
           } else if (s === 'answered') {
              circleClass = "bg-[var(--admin-primary)] border-[3px] border-[var(--admin-primary)] rounded-[14px]";
              textClass = "text-[var(--admin-primary-contrast)]";
           } else if (s === 'skipped') {
              circleClass = "border-[3px] border-[var(--admin-danger)] bg-transparent rounded-2xl opacity-60";
              textClass = "text-[var(--admin-danger)]";
           } else { // unvisited
              circleClass = "bg-[var(--admin-card-soft)] border-2 border-[var(--admin-border)] rounded-2xl cursor-default";
              textClass = "text-[var(--admin-muted)] opacity-50";
           }

           return (
             <motion.button 
               whileTap={{ scale: 0.9 }}
               key={`${step.id}-${index}`} 
               type="button"
               disabled={isSubmitting}
               className={cn(
                 "flex relative items-center justify-center w-11 h-11 font-black text-sm transition-all duration-300 ease-spring",
                  circleClass,
                 !isSubmitting && s !== 'unvisited' && "hover:scale-105 cursor-pointer shadow-sm hover:shadow-md",
                 "focus-visible:ring-2 focus-visible:ring-[var(--admin-primary)] focus-visible:ring-offset-2 focus-visible:ring-offset-[var(--admin-bg)]"
               )}
               onClick={() => {
                 if (index !== currentStep && !isSubmitting && s !== 'unvisited') {
                    handleSetStep(index, index > currentStep ? 1 : -1);
                 }
               }}
               title={step.title || `سؤال ${index + 1}`}
               aria-label={`سؤال ${index + 1}`}
               aria-current={isCurrent ? "step" : undefined}
             >
               {s === 'answered' ? (
                 <Check className="w-5 h-5 text-[var(--admin-primary-contrast)] animate-in zoom-in" strokeWidth={3} />
               ) : (
                 <span className={textClass}>{index + 1}</span>
               )}
               {s === 'skipped' && (
                 <div className="absolute -top-1 -right-1 w-3 h-3 bg-[var(--admin-danger)] rounded-full border-2 border-[var(--admin-background)] shadow-sm" />
               )}
             </motion.button>
           );
        })}
      </div>

      {/* Progress Text */}
      <div className="text-sm font-black text-[var(--admin-primary)] uppercase tracking-wider mb-2">
        سؤال {currentStep + 1} من {steps.length}
      </div>

      {/* Content Area */}
      <div className="relative overflow-hidden min-h-[350px]">
        <AnimatePresence mode="wait" custom={direction}>
          <motion.div
            key={currentStep}
            custom={direction}
            variants={variants}
            initial="initial"
            animate="active"
            exit="exit"
            className="w-full"
          >
            {steps[currentStep]?.content}
          </motion.div>
        </AnimatePresence>
      </div>

      {/* Navigation Controls */}
      <div className="flex items-center justify-between pt-6 border-t border-[var(--admin-border)]">
        <button
          onClick={goToPrevStep}
          disabled={currentStep === 0 || isSubmitting}
          type="button"
          className="flex items-center gap-2 px-6 py-3 rounded-[16px] border border-[var(--admin-border)] bg-[var(--admin-card)] hover:bg-[var(--admin-border)] text-[var(--admin-text)] disabled:opacity-40 disabled:hover:bg-[var(--admin-card)] font-black transition-all focus-visible:ring-2 focus-visible:ring-[var(--admin-primary)] focus-visible:ring-offset-2 focus-visible:ring-offset-[var(--admin-bg)]"
        >
          <ChevronRight className="w-5 h-5" /> <span>السابق</span>
        </button>

        <button
          onClick={goToNextStep}
          disabled={isSubmitting}
          type="button"
          className="flex items-center gap-2 px-8 py-3.5 rounded-2xl bg-[var(--admin-primary)] hover:bg-[var(--admin-primary-strong)] text-[var(--admin-primary-contrast)] font-black text-lg shadow-[0_8px_20px_rgba(119,90,25,0.2)] hover:-translate-y-1 hover:shadow-[0_12px_25px_rgba(119,90,25,0.3)] transition-all focus-visible:ring-2 focus-visible:ring-[var(--admin-primary)] focus-visible:ring-offset-2 focus-visible:ring-offset-[var(--admin-bg)] disabled:opacity-50 disabled:hover:translate-y-0"
        >
          {currentStep === steps.length - 1 ? (
             isSubmitting ? "جاري التسليم..." : <span className="flex items-center">تسليم وإنهاء <Check className="mr-2 w-6 h-6" /></span>
          ) : (
             <span className="flex items-center">التالي <ChevronLeft className="mr-2 w-6 h-6" /></span>
          )}
        </button>
      </div>
    </div>
  );
}
