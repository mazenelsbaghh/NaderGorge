'use client';

import { motion } from "framer-motion";

type SectionHeadingProps = {
  eyebrow: string;
  title: string;
  description: string;
  align?: "center" | "start";
};

export function SectionHeading({
  eyebrow,
  title,
  description,
  align = "center",
}: SectionHeadingProps) {
  const alignment = align === "center" ? "text-center items-center" : "text-right items-start";
  
  const itemVariants = {
    hidden: { opacity: 0, y: 20 },
    visible: { opacity: 1, y: 0, transition: { duration: 0.5, ease: [0.25, 1, 0.5, 1] as const } }
  };

  return (
    <motion.div 
      initial="hidden"
      whileInView="visible"
      viewport={{ once: true, margin: "-50px" }}
      variants={{
        hidden: {},
        visible: { transition: { staggerChildren: 0.1 } }
      }}
      className={`flex max-w-2xl flex-col gap-4 ${alignment}`}
    >
      <motion.span variants={itemVariants} className="landing-chip">{eyebrow}</motion.span>
      <div className="space-y-3">
        <motion.h2 variants={itemVariants} className="text-3xl font-black tracking-tight text-[var(--landing-ink)] md:text-5xl">
          {title}
        </motion.h2>
        <motion.p variants={itemVariants} className="text-base leading-8 text-[var(--landing-muted)] md:text-lg">
          {description}
        </motion.p>
      </div>
    </motion.div>
  );
}

