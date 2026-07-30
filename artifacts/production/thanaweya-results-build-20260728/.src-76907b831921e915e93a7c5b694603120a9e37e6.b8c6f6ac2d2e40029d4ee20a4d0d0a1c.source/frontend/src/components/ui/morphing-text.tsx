"use client";

import React, { useState, useEffect } from "react";
import { motion, AnimatePresence } from "framer-motion";
import { cn } from "@/lib/utils";

export interface MorphingTextProps {
  className?: string;
  texts: string[];
}

export const MorphingText: React.FC<MorphingTextProps> = ({
  texts,
  className,
}) => {
  const [index, setIndex] = useState(0);

  useEffect(() => {
    if (!texts || texts.length <= 1) return;

    const interval = setInterval(() => {
      setIndex((prev) => (prev + 1) % texts.length);
    }, 2800);

    return () => clearInterval(interval);
  }, [texts]);

  if (!texts || texts.length === 0) return null;

  return (
    <div
      className={cn(
        "relative h-12 w-full font-sans text-3xl leading-[1.2] font-black md:h-16 lg:text-6xl overflow-hidden",
        className
      )}
    >
      <AnimatePresence mode="wait">
        <motion.span
          key={texts[index] || index}
          initial={{ opacity: 0, y: 15 }}
          animate={{ opacity: 1, y: 0 }}
          exit={{ opacity: 0, y: -15 }}
          transition={{ duration: 0.5, ease: "easeInOut" }}
          className="absolute inset-x-0 top-0 inline-block w-full text-right"
        >
          {texts[index]}
        </motion.span>
      </AnimatePresence>
    </div>
  );
};
