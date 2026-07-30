"use client"

import React from "react"
import { motion, type MotionProps } from "framer-motion"
import Link from "next/link"

import { cn } from "@/lib/utils"

const animationProps: MotionProps = {
  initial: { "--x": "100%", scale: 0.8 },
  animate: { "--x": "-100%", scale: 1 },
  whileTap: { scale: 0.95 },
  transition: {
    repeat: Infinity,
    repeatType: "loop",
    repeatDelay: 1,
    type: "spring",
    stiffness: 20,
    damping: 15,
    mass: 2,
    scale: {
      type: "spring",
      stiffness: 200,
      damping: 5,
      mass: 0.5,
    },
  },
}

interface ShinyButtonProps
  extends
    Omit<React.ButtonHTMLAttributes<HTMLButtonElement>, keyof MotionProps>,
    MotionProps {
  children: React.ReactNode
  className?: string
  href?: string
}

export const ShinyButton = React.forwardRef<
  HTMLButtonElement & HTMLAnchorElement,
  ShinyButtonProps
>(({ children, className, href, ...props }, ref) => {
  const commonClassName = cn(
    "relative inline-block cursor-pointer rounded-lg border px-6 py-2 font-medium backdrop-blur-xl transition-shadow duration-300 ease-in-out hover:shadow",
    "bg-[radial-gradient(circle_at_50%_0%,color-mix(in_srgb,var(--landing-accent)_10%,transparent)_0%,transparent_60%)]",
    "hover:shadow-[0_0_20px_color-mix(in_srgb,var(--landing-accent)_10%,transparent)]",
    className
  );

  const innerContent = (
    <>
      <span
        className="relative block size-full tracking-wide text-[var(--landing-ink)] font-bold dark:font-semibold"
        style={{
          maskImage:
            "linear-gradient(-75deg,var(--landing-ink) calc(var(--x) + 20%),transparent calc(var(--x) + 30%),var(--landing-ink) calc(var(--x) + 100%))",
        }}
      >
        {children}
      </span>
      <span
        style={{
          mask: "linear-gradient(rgb(0,0,0), rgb(0,0,0)) content-box exclude,linear-gradient(rgb(0,0,0), rgb(0,0,0))",
          WebkitMask:
            "linear-gradient(rgb(0,0,0), rgb(0,0,0)) content-box exclude,linear-gradient(rgb(0,0,0), rgb(0,0,0))",
          backgroundImage:
            "linear-gradient(-75deg, color-mix(in srgb, var(--landing-accent) 20%, transparent) calc(var(--x)+20%), color-mix(in srgb, var(--landing-accent) 80%, transparent) calc(var(--x)+25%), color-mix(in srgb, var(--landing-accent) 20%, transparent) calc(var(--x)+100%))",
        }}
        className="absolute inset-0 z-10 block rounded-[inherit] p-px"
      />
    </>
  );

  if (href) {
    return (
      <Link href={href} tabIndex={props.tabIndex} className="inline-block outline-none w-fit h-fit">
        <motion.div
          ref={ref as any}
          className={commonClassName}
          {...animationProps}
          {...(props as any)}
        >
          {innerContent}
        </motion.div>
      </Link>
    );
  }

  return (
    <motion.button
      ref={ref}
      className={commonClassName}
      {...animationProps}
      {...props}
    >
      {innerContent}
    </motion.button>
  )
})

ShinyButton.displayName = "ShinyButton"
