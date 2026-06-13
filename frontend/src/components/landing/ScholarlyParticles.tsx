"use client";

import { useEffect, useRef, useState } from "react";

interface Particle {
  x: number;
  y: number;
  vx: number;
  vy: number;
  radius: number;
  alpha: number;
  targetAlpha: number;
  color: string;
}

export function ScholarlyParticles() {
  const [isEnabled, setIsEnabled] = useState(false);
  const canvasRef = useRef<HTMLCanvasElement>(null);
  const containerRef = useRef<HTMLDivElement>(null);
  const particlesRef = useRef<Particle[]>([]);
  const animationFrameRef = useRef<number | null>(null);
  const isVisibleRef = useRef<boolean>(true);
  const mouseRef = useRef<{ x: number; y: number; active: boolean }>({
    x: 0,
    y: 0,
    active: false,
  });

  // Track colors dynamically based on CSS theme variables
  const colorsRef = useRef<{ primary: string; secondary: string; accent: string }>({
    primary: "#0A1D3D",
    secondary: "#0E8F8F",
    accent: "#D4A017",
  });

  useEffect(() => {
    const constrainedContext = window.matchMedia(
      "(max-width: 767px), (pointer: coarse), (prefers-reduced-motion: reduce), (update: slow)",
    );

    const updateAvailability = () => {
      const connection = (
        navigator as Navigator & { connection?: { saveData?: boolean } }
      ).connection;
      setIsEnabled(!constrainedContext.matches && !connection?.saveData);
    };

    updateAvailability();
    constrainedContext.addEventListener("change", updateAvailability);

    return () => {
      constrainedContext.removeEventListener("change", updateAvailability);
    };
  }, []);

  useEffect(() => {
    if (!isEnabled) return;

    const canvas = canvasRef.current;
    if (!canvas) return;

    const ctx = canvas.getContext("2d");
    if (!ctx) return;

    // Update dimensions
    const resizeCanvas = () => {
      const rect = containerRef.current?.getBoundingClientRect() || {
        width: window.innerWidth,
        height: window.innerHeight,
      };
      canvas.width = rect.width;
      canvas.height = rect.height;
      initParticles(rect.width, rect.height);
    };

    // Extract colors from computed CSS variables
    const updateColors = () => {
      if (typeof window === "undefined") return;
      const styles = getComputedStyle(document.documentElement);
      colorsRef.current = {
        primary: styles.getPropertyValue("--primary").trim() || "#0A1D3D",
        secondary: styles.getPropertyValue("--secondary").trim() || "#0E8F8F",
        accent: styles.getPropertyValue("--accent").trim() || "#D4A017",
      };
    };

    // Watch for theme / DOM attribute changes to update colors
    const observer = new MutationObserver(() => {
      updateColors();
    });
    observer.observe(document.documentElement, {
      attributes: true,
      attributeFilter: ["class", "data-student-theme-surface", "style"],
    });

    updateColors();
    resizeCanvas();
    window.addEventListener("resize", resizeCanvas);

    // Mouse events
    const handleMouseMove = (e: MouseEvent) => {
      const rect = canvas.getBoundingClientRect();
      // Support RTL page layouts or scroll offset
      mouseRef.current = {
        x: e.clientX - rect.left,
        y: e.clientY - rect.top,
        active: true,
      };
    };

    const handleMouseLeave = () => {
      mouseRef.current.active = false;
    };

    window.addEventListener("mousemove", handleMouseMove);
    document.addEventListener("mouseleave", handleMouseLeave);

    // Intersection Observer to stop drawing when scrolled off-screen
    const intersectionObserver = new IntersectionObserver(
      (entries) => {
        entries.forEach((entry) => {
          isVisibleRef.current = entry.isIntersecting;
          if (entry.isIntersecting) {
            startAnimation();
          } else {
            stopAnimation();
          }
        });
      },
      { threshold: 0.05 }
    );

    if (containerRef.current) {
      intersectionObserver.observe(containerRef.current);
    }

    // Particle Initialization
    function initParticles(width: number, height: number) {
      const particleCount = Math.min(Math.floor((width * height) / 18000), 80);
      const tempParticles: Particle[] = [];

      for (let i = 0; i < particleCount; i++) {
        // Distribute colors: 60% primary/secondary, 40% accent
        const rand = Math.random();
        let color = colorsRef.current.secondary;
        if (rand < 0.3) {
          color = colorsRef.current.primary;
        } else if (rand > 0.7) {
          color = colorsRef.current.accent;
        }

        tempParticles.push({
          x: Math.random() * width,
          y: Math.random() * height,
          vx: (Math.random() - 0.5) * 0.35,
          vy: (Math.random() - 0.5) * 0.35,
          radius: Math.random() * 2.5 + 1.2,
          alpha: Math.random() * 0.3 + 0.1,
          targetAlpha: Math.random() * 0.4 + 0.2,
          color,
        });
      }
      particlesRef.current = tempParticles;
    }

    // Convert hex color to rgba helper
    function hexToRgba(hex: string, alpha: number): string {
      // Clean hex string
      const cleanHex = hex.replace("#", "").trim();
      if (cleanHex.length !== 6 && cleanHex.length !== 3) {
        return `rgba(14, 143, 143, ${alpha})`; // Fallback teal
      }
      
      let r = 0, g = 0, b = 0;
      if (cleanHex.length === 6) {
        r = parseInt(cleanHex.substring(0, 2), 16);
        g = parseInt(cleanHex.substring(2, 4), 16);
        b = parseInt(cleanHex.substring(4, 6), 16);
      } else {
        r = parseInt(cleanHex[0] + cleanHex[0], 16);
        g = parseInt(cleanHex[1] + cleanHex[1], 16);
        b = parseInt(cleanHex[2] + cleanHex[2], 16);
      }
      return `rgba(${r}, ${g}, ${b}, ${alpha})`;
    }

    // Animation Loop
    function animate() {
      if (!ctx || !canvas || !isVisibleRef.current) return;

      ctx.clearRect(0, 0, canvas.width, canvas.height);
      const width = canvas.width;
      const height = canvas.height;
      const particles = particlesRef.current;
      const mouse = mouseRef.current;

      // Update and draw particles
      particles.forEach((p) => {
        // Ambient movement
        p.x += p.vx;
        p.y += p.vy;

        // Interaction with mouse
        if (mouse.active) {
          const dx = mouse.x - p.x;
          const dy = mouse.y - p.y;
          const dist = Math.hypot(dx, dy);
          const maxDist = 180;

          if (dist < maxDist) {
            // Apply a gentle force towards the cursor
            const force = (maxDist - dist) / maxDist;
            p.x += (dx / dist) * force * 0.45;
            p.y += (dy / dist) * force * 0.45;
            p.alpha = p.alpha * 0.9 + p.targetAlpha * 1.5 * force * 0.1;
          } else {
            p.alpha = p.alpha * 0.95 + p.targetAlpha * 0.05;
          }
        } else {
          p.alpha = p.alpha * 0.95 + p.targetAlpha * 0.05;
        }

        // Keep inside bounds with wrapping
        if (p.x < -10) p.x = width + 10;
        else if (p.x > width + 10) p.x = -10;
        
        if (p.y < -10) p.y = height + 10;
        else if (p.y > height + 10) p.y = -10;

        // Draw particle
        ctx.beginPath();
        ctx.arc(p.x, p.y, p.radius, 0, Math.PI * 2);
        ctx.fillStyle = hexToRgba(p.color, p.alpha);
        ctx.fill();
      });

      // Draw connections (subtle "intellectual network" connections)
      ctx.strokeStyle = hexToRgba(colorsRef.current.secondary, 0.06);
      ctx.lineWidth = 0.65;
      for (let i = 0; i < particles.length; i++) {
        for (let j = i + 1; j < particles.length; j++) {
          const p1 = particles[i];
          const p2 = particles[j];
          const dist = Math.hypot(p1.x - p2.x, p1.y - p2.y);
          const maxConnectionDist = 120;

          if (dist < maxConnectionDist) {
            const alpha = (1 - dist / maxConnectionDist) * 0.09 * Math.min(p1.alpha, p2.alpha);
            ctx.beginPath();
            ctx.moveTo(p1.x, p1.y);
            ctx.lineTo(p2.x, p2.y);
            ctx.strokeStyle = hexToRgba(
              p1.color === colorsRef.current.accent ? colorsRef.current.accent : colorsRef.current.secondary,
              alpha
            );
            ctx.stroke();
          }
        }
      }

      animationFrameRef.current = requestAnimationFrame(animate);
    }

    function startAnimation() {
      if (!animationFrameRef.current) {
        animationFrameRef.current = requestAnimationFrame(animate);
      }
    }

    function stopAnimation() {
      if (animationFrameRef.current) {
        cancelAnimationFrame(animationFrameRef.current);
        animationFrameRef.current = null;
      }
    }

    startAnimation();

    return () => {
      stopAnimation();
      window.removeEventListener("resize", resizeCanvas);
      window.removeEventListener("mousemove", handleMouseMove);
      document.removeEventListener("mouseleave", handleMouseLeave);
      observer.disconnect();
      intersectionObserver.disconnect();
    };
  }, [isEnabled]);

  if (!isEnabled) return null;

  return (
    <div
      ref={containerRef}
      className="pointer-events-none absolute inset-0 select-none overflow-hidden"
      style={{ zIndex: 0 }}
    >
      <canvas ref={canvasRef} className="block h-full w-full opacity-60 dark:opacity-45" />
    </div>
  );
}
