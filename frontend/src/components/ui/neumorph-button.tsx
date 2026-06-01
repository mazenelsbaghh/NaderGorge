"use client"

import * as React from "react"
import { cva, type VariantProps } from "class-variance-authority"
import { Loader2 } from "lucide-react"
import { motion, type HTMLMotionProps } from "framer-motion"

const buttonVariants = cva(
  "justify-center text-sm font-bold items-center transition-[box-shadow,background-color,filter] disabled:cursor-not-allowed disabled:opacity-50 flex active:transition-none cursor-pointer select-none [&_svg]:shrink-0 [&_svg:not([class*='h-'])]:size-4 gap-2",
  {
    variants: {
      intent: {
        primary: [
          "bg-[var(--admin-primary)]",
          "text-[var(--admin-primary-contrast)]",
          "[box-shadow:inset_0px_-2px_0px_0px_var(--admin-primary-strong),_0px_1px_6px_0px_var(--admin-shadow)]",
          "hover:enabled:[box-shadow:inset_0px_-2.5px_0px_0px_var(--admin-primary-strong),_0px_2px_10px_0px_var(--admin-shadow)]",
          "hover:enabled:brightness-110",
          "active:[box-shadow:inset_0px_-1px_0px_0px_var(--admin-primary-strong),_0px_0.5px_2px_0px_var(--admin-shadow)]",
          "disabled:shadow-none",
        ],
        ghost: [
          "bg-[var(--admin-card)]",
          "text-[var(--admin-text)]",
          "border border-[var(--admin-border)]",
          "[box-shadow:inset_0px_-1.5px_0px_0px_var(--admin-border),_0px_1px_4px_0px_var(--admin-shadow)]",
          "hover:enabled:[box-shadow:inset_0px_-2px_0px_0px_var(--admin-border),_0px_1.5px_6px_0px_var(--admin-shadow)]",
          "hover:enabled:bg-[var(--admin-hover)]",
          "active:[box-shadow:inset_0px_-0.5px_0px_0px_var(--admin-border),_0px_0.5px_1px_0px_var(--admin-shadow)]",
          "disabled:shadow-none",
        ],
        danger: [
          "bg-[var(--admin-danger)]",
          "text-white",
          "[box-shadow:inset_0px_-2px_0px_0px_color-mix(in_srgb,var(--admin-danger)_65%,black),_0px_1px_6px_0px_var(--admin-danger-10)]",
          "hover:enabled:[box-shadow:inset_0px_-2.5px_0px_0px_color-mix(in_srgb,var(--admin-danger)_65%,black),_0px_2px_10px_0px_var(--admin-danger-10)]",
          "hover:enabled:brightness-110",
          "active:[box-shadow:inset_0px_-1px_0px_0px_color-mix(in_srgb,var(--admin-danger)_65%,black),_0px_0.5px_2px_0px_var(--admin-danger-10)]",
          "disabled:shadow-none",
        ],
        icon: [
          "bg-[var(--admin-card)]",
          "text-[var(--admin-muted)]",
          "border border-[var(--admin-border)]",
          "[box-shadow:inset_0px_-1px_0px_0px_var(--admin-border),_0px_1px_3px_0px_var(--admin-shadow)]",
          "hover:enabled:[box-shadow:inset_0px_-1.5px_0px_0px_var(--admin-border),_0px_1.5px_5px_0px_var(--admin-shadow)]",
          "hover:enabled:bg-[var(--admin-primary-15)]",
          "hover:enabled:text-[var(--admin-primary)]",
          "active:[box-shadow:none]",
          "disabled:shadow-none",
        ],
      },
      size: {
        sm: ["text-xs", "py-1.5", "px-3", "h-8", "rounded-[10px]"],
        md: ["text-sm", "py-2", "px-5", "h-10", "rounded-[10px]"],
        lg: ["text-sm", "py-2.5", "px-6", "h-12", "rounded-[12px]"],
        xl: ["text-base", "py-3", "px-8", "h-14", "rounded-[14px]"],
        icon: ["h-9", "w-9", "p-0", "rounded-[10px]"],
      },
      pill: {
        true: "!rounded-full",
      },
      fullWidth: {
        true: "w-full",
      },
    },
    defaultVariants: {
      intent: "primary",
      size: "md",
    },
  }
)

export interface NeumorphButtonProps
  extends HTMLMotionProps<"button">,
    VariantProps<typeof buttonVariants> {
  children: React.ReactNode
  loading?: boolean
  asChild?: boolean
}

const NeumorphButton: React.FC<NeumorphButtonProps> = ({
  className,
  intent,
  size,
  fullWidth,
  pill,
  children,
  loading = false,
  asChild = false,
  disabled,
  ...props
}) => {
  const classes = buttonVariants({ intent, size, fullWidth, pill, className })

  if (asChild) {
    const child = React.Children.only(children)

    if (!React.isValidElement(child)) {
      return null
    }

    const childProps = child.props as {
      className?: string
      children?: React.ReactNode
      onClick?: React.MouseEventHandler
      tabIndex?: number
    }

    return React.cloneElement(child as React.ReactElement<any>, {
      ...props,
      className: [classes, childProps.className].filter(Boolean).join(" "),
      "aria-disabled": disabled || loading || undefined,
      tabIndex: disabled || loading ? -1 : childProps.tabIndex,
      onClick: disabled || loading
        ? (event: React.MouseEvent) => {
            event.preventDefault()
            event.stopPropagation()
          }
        : childProps.onClick,
      children: (
        <span className="flex items-center gap-2">
          {loading ? <Loader2 className="h-4 w-4 animate-spin" /> : null}
          <motion.span
            initial={{ opacity: 1 }}
            animate={{ opacity: loading ? 0.7 : 1 }}
            transition={{ duration: 0.2 }}
            className="flex items-center gap-2"
          >
            {childProps.children}
          </motion.span>
        </span>
      ),
    })
  }

  return (
    <motion.button
      className={classes}
      disabled={disabled || loading}
      whileTap={{ scale: 0.97 }}
      whileHover={{ scale: 1.015 }}
      transition={{ type: "spring", stiffness: 450, damping: 15 }}
      {...props}
    >
      {loading ? <Loader2 className="h-4 w-4 animate-spin" /> : null}
      <motion.span
        initial={{ opacity: 1 }}
        animate={{ opacity: loading ? 0.7 : 1 }}
        transition={{ duration: 0.2 }}
        className="flex items-center gap-2"
      >
        {children}
      </motion.span>
    </motion.button>
  )
}

export default NeumorphButton
