'use client';

import type { CSSProperties, HTMLAttributes, ReactNode } from "react";

import { cn } from "@/lib/utils";

type MarqueeProps = HTMLAttributes<HTMLDivElement> & {
  reverse?: boolean;
  pauseOnHover?: boolean;
  vertical?: boolean;
  children: ReactNode;
  repeat?: number;
};

export function Marquee({
  className,
  reverse = false,
  pauseOnHover = false,
  vertical = false,
  children,
  repeat = 4,
  style,
  ...props
}: MarqueeProps) {
  const customStyle = {
    ...style,
    "--marquee-repeat": repeat,
  } as CSSProperties;

  const repetitions = Array.from({ length: repeat }, (_, index) => (
    <div
      key={index}
      className={cn("flex shrink-0 gap-4", vertical ? "flex-col" : "flex-row")}
    >
      {children}
    </div>
  ));

  return (
    <div
      {...props}
      style={customStyle}
      className={cn(
        "group relative flex overflow-hidden",
        vertical ? "h-full flex-col" : "w-full flex-row",
        className
      )}
    >
      <div
        className={cn(
          "flex min-w-max shrink-0 gap-4 will-change-transform",
          vertical ? "animate-marquee-vertical flex-col" : "animate-marquee-horizontal flex-row",
          reverse && "[animation-direction:reverse]",
          pauseOnHover && "group-hover:[animation-play-state:paused]"
        )}
      >
        {repetitions}
      </div>
    </div>
  );
}
