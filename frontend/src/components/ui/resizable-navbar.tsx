"use client";
import React, { useRef, useState } from "react";
import { cn } from "@/lib/utils";
import { Menu, X } from "lucide-react";
import {
  motion,
  AnimatePresence,
  useScroll,
  useMotionValueEvent,
} from "framer-motion";
import Link from "next/link";

interface NavbarProps {
  children: React.ReactNode;
  className?: string;
  isLanding?: boolean;
}

interface NavBodyProps {
  children: React.ReactNode;
  className?: string;
  visible?: boolean;
  isLanding?: boolean;
}

interface NavItemsProps {
  items: {
    name: string;
    link: string;
  }[];
  className?: string;
  onItemClick?: () => void;
}

interface MobileNavProps {
  children: React.ReactNode;
  className?: string;
  visible?: boolean;
}

interface MobileNavHeaderProps {
  children: React.ReactNode;
  className?: string;
}

interface MobileNavMenuProps {
  children: React.ReactNode;
  className?: string;
  isOpen: boolean;
  onClose: () => void;
}

export const Navbar = ({ children, className, isLanding }: NavbarProps) => {
  const { scrollY } = useScroll(); // Track window scroll, not fixed element
  const [visible, setVisible] = useState<boolean>(!isLanding);

  useMotionValueEvent(scrollY, "change", (latest) => {
    if (!isLanding) {
      setVisible(true);
      return;
    }
    if (latest > 100) {
      setVisible(true);
    } else {
      setVisible(false);
    }
  });

  return (
    <motion.div
      // Use fixed top-0 to ensure it stays exactly at the top
      className={cn("fixed top-0 inset-x-0 z-50 w-full pt-4 pointer-events-none", className)}
    >
      {React.Children.map(children, (child) =>
        React.isValidElement(child)
          ? React.cloneElement(
              child as React.ReactElement<{ visible?: boolean }>,
              { visible },
            )
          : child,
      )}
    </motion.div>
  );
};

export const NavBody = ({ children, className, visible }: NavBodyProps) => {
  return (
    <motion.div
      initial={{
        width: visible ? "1050px" : "100%",
        y: visible ? 8 : 0,
      }}
      animate={{
        backdropFilter: visible ? "blur(10px)" : "none",
        boxShadow: visible
          ? "0 4px 20px rgba(0, 0, 0, 0.08)"
          : "none",
        width: visible ? "1050px" : "100%",
        y: visible ? 8 : 0,
      }}
      transition={{
        type: "spring",
        stiffness: 200,
        damping: 50,
      }}
      className={cn(
        "relative z-[60] mx-auto hidden w-[min(1180px,92vw)] flex-row items-center justify-between self-start rounded-full px-4 py-3 lg:flex transition-colors duration-300 gap-4 pointer-events-auto",
        visible && "bg-[var(--landing-card-strong)] border border-[var(--landing-line)] backdrop-blur-3xl shadow-[0_8px_32px_rgba(0,0,0,0.12)]",
        !visible && "bg-transparent border border-transparent",
        className,
      )}
    >
      {children}
    </motion.div>
  );
};

export const NavItems = ({ items, className, onItemClick }: NavItemsProps) => {
  const [hovered, setHovered] = useState<number | null>(null);

  return (
    <motion.div
      onMouseLeave={() => setHovered(null)}
      className={cn(
        "hidden flex-1 flex-row items-center justify-center space-x-2 space-x-reverse text-sm font-bold text-[var(--landing-muted)] transition duration-200 lg:flex lg:space-x-2",
        className,
      )}
    >
      {items.map((item, idx) => (
        <Link
          onMouseEnter={() => setHovered(idx)}
          onClick={onItemClick}
          className="relative px-4 py-2 text-[var(--landing-ink)]"
          key={`link-${idx}`}
          href={item.link}
        >
          {hovered === idx && (
            <motion.div
              layoutId="hovered"
              className="absolute inset-0 h-full w-full rounded-full bg-[var(--landing-card-strong)] opacity-50"
            />
          )}
          <span className="relative z-20 transition hover:text-[var(--landing-accent)] whitespace-nowrap">{item.name}</span>
        </Link>
      ))}
    </motion.div>
  );
};

export const MobileNav = ({ children, className, visible }: MobileNavProps) => {
  return (
    <motion.div
      initial={{
        width: visible ? "95%" : "100%",
        borderRadius: visible ? "1rem" : "2rem",
        y: visible ? 8 : 0,
      }}
      animate={{
        backdropFilter: visible ? "blur(10px)" : "none",
        boxShadow: visible
          ? "0 4px 20px rgba(0, 0, 0, 0.08)"
          : "none",
        width: visible ? "95%" : "100%",
        borderRadius: visible ? "1rem" : "2rem",
        y: visible ? 8 : 0,
      }}
      transition={{
        type: "spring",
        stiffness: 200,
        damping: 50,
      }}
      className={cn(
        "relative z-50 mx-auto flex w-[min(1180px,92vw)] flex-col items-center justify-between px-4 py-3 lg:hidden transition-colors duration-300 pointer-events-auto",
        visible && "bg-[var(--landing-card-strong)] border border-[var(--landing-line)] backdrop-blur-3xl shadow-[0_8px_32px_rgba(0,0,0,0.12)]",
        !visible && "bg-transparent border border-transparent",
        className,
      )}
    >
      {children}
    </motion.div>
  );
};

export const MobileNavHeader = ({
  children,
  className,
}: MobileNavHeaderProps) => {
  return (
    <div
      className={cn(
        "flex w-full flex-row items-center justify-between",
        className,
      )}
    >
      {children}
    </div>
  );
};

export const MobileNavMenu = ({
  children,
  className,
  isOpen,
  onClose,
}: MobileNavMenuProps) => {
  return (
    <AnimatePresence>
      {isOpen && (
        <motion.div
          initial={{ opacity: 0, scale: 0.95 }}
          animate={{ opacity: 1, scale: 1 }}
          exit={{ opacity: 0, scale: 0.95 }}
          transition={{ duration: 0.2 }}
          className={cn(
            "absolute inset-x-0 top-16 z-50 flex w-full flex-col items-start justify-start gap-4 rounded-xl border border-[var(--landing-line)] bg-[var(--landing-card-strong)] px-4 py-8 shadow-xl backdrop-blur-3xl",
            className,
          )}
        >
          {children}
        </motion.div>
      )}
    </AnimatePresence>
  );
};

export const MobileNavToggle = ({
  isOpen,
  onClick,
}: {
  isOpen: boolean;
  onClick: () => void;
}) => {
  return isOpen ? (
    <X className="text-[var(--landing-ink)] h-6 w-6 cursor-pointer" onClick={onClick} />
  ) : (
    <Menu className="text-[var(--landing-ink)] h-6 w-6 cursor-pointer" onClick={onClick} />
  );
};

export const NavbarLogo = () => {
  return (
    <Link href="/" className="relative z-20 flex items-center gap-2 sm:gap-3 ml-4">
      <div
        className="flex h-9 w-9 sm:h-11 sm:w-11 items-center justify-center rounded-full text-sm sm:text-lg text-[var(--landing-accent)] shadow-[0_2px_10px_rgba(0,0,0,0.1)_inset] border border-[var(--landing-line)] bg-[var(--landing-card-strong)]"
      >
        ☥
      </div>
      <div>
        <p className="text-[10px] sm:text-xs font-semibold tracking-[0.2em] sm:tracking-[0.3em] text-[var(--landing-muted)] leading-tight">
          NADER GORGE
        </p>
        <p className="text-sm sm:text-base font-black text-[var(--landing-ink)] md:text-lg leading-none mt-0.5 sm:mt-1">
          الأستاذ نادر جورج
        </p>
      </div>
    </Link>
  );
};

interface NavbarButtonProps extends React.ButtonHTMLAttributes<HTMLButtonElement> {
  href?: string;
  variant?: "primary" | "secondary" | "danger";
}

export const NavbarButton = ({
  href,
  children,
  className,
  variant = "primary",
  ...props
}: NavbarButtonProps) => {
  const baseStyles =
    "px-5 py-2.5 rounded-full text-sm font-extrabold relative cursor-pointer hover:-translate-y-0.5 transition inline-flex items-center justify-center";

  const variantStyles = {
    primary:
      "bg-[var(--landing-accent)] text-[var(--landing-accent-foreground)] shadow-[0_10px_24px_rgba(145,95,42,0.28)] hover:bg-[var(--landing-accent-strong)]",
    secondary:
      "text-[var(--landing-ink)] hover:bg-[var(--landing-card-strong)] border border-[var(--landing-line)] shadow-sm",
    danger:
      "text-red-500 hover:text-red-400 hover:bg-[var(--landing-card-strong)] border border-[var(--landing-line)] shadow-sm",
  };

  const combinedClassName = cn(baseStyles, variantStyles[variant], className);

  if (href) {
    return (
      <Link href={href} className={combinedClassName} onClick={props.onClick as any}>
        {children}
      </Link>
    );
  }

  return (
    <button className={combinedClassName} {...props}>
      {children}
    </button>
  );
};
