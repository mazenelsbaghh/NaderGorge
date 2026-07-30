import React from "react"
import { ArrowLeft } from "lucide-react"
import Link from "next/link"

import { cn } from "@/lib/utils"

interface InteractiveHoverButtonProps extends React.ButtonHTMLAttributes<HTMLButtonElement> {
  icon?: React.ReactNode;
  href?: string;
  hoverEffect?: boolean;
}

export const InteractiveHoverButton = React.forwardRef<
  HTMLButtonElement,
  InteractiveHoverButtonProps
>(({ children, className, icon, href, hoverEffect = true, ...props }, ref) => {
  const commonClassName = cn(
    "group/ihb relative w-auto cursor-pointer overflow-hidden rounded-full border border-[var(--btn-border,var(--landing-line))] p-2 px-6 text-center font-bold bg-[var(--btn-bg,transparent)] text-[var(--btn-text,var(--landing-ink))] transition-colors hover:border-[var(--btn-primary,var(--landing-accent))] disabled:cursor-not-allowed disabled:opacity-60",
    className
  );

  const innerContent = hoverEffect ? (
    <>
      <div className="flex items-center justify-center gap-2">
        <div className="bg-[var(--btn-primary,var(--landing-accent))] h-3 w-3 rounded-full transition-all duration-300 group-hover/ihb:scale-[100.8]"></div>
        <span className="inline-block transition-all duration-300 group-hover/ihb:-translate-x-12 group-hover/ihb:opacity-0">
          {children}
        </span>
      </div>
      <div className="text-[var(--btn-fg,var(--landing-accent-foreground))] absolute top-0 z-10 flex h-full w-full -translate-x-12 items-center justify-center gap-2 opacity-0 transition-all duration-300 group-hover/ihb:translate-x-3 group-hover/ihb:opacity-100 pr-5">
        <span>{children}</span>
        {icon || <ArrowLeft className="h-4 w-4" />}
      </div>
    </>
  ) : (
    <div className="flex items-center justify-center gap-2">
      <div className="bg-[var(--btn-primary,var(--landing-accent))] h-2.5 w-2.5 rounded-full" />
      <span>{children}</span>
      {icon ? <span className="text-[var(--btn-fg,var(--landing-accent-foreground))]">{icon}</span> : null}
    </div>
  );

  if (href) {
    return (
      <Link href={href} className={commonClassName} tabIndex={props.tabIndex}>
        {innerContent}
      </Link>
    );
  }

  return (
    <button ref={ref} className={commonClassName} {...props}>
      {innerContent}
    </button>
  )
})
InteractiveHoverButton.displayName = "InteractiveHoverButton"
