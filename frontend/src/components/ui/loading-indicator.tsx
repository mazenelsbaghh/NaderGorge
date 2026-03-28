import React from 'react';

/**
 * FullScreenLoader: A modern, non-spinner loading state using elegant glowing pulses.
 * Used for full-page or section-level loading transitions.
 */
export function FullScreenLoader({ className = '' }: { className?: string }) {
  return (
    <div className={`flex min-h-[50vh] flex-col items-center justify-center ${className}`}>
      <div className="relative flex flex-col items-center justify-center">
        {/* Glow backdrop */}
        <div className="absolute h-32 w-32 animate-[ping_2s_cubic-bezier(0,0,0.2,1)_infinite] rounded-full bg-current opacity-10 blur-xl" />
        
        {/* Center node */}
        <div className="relative flex h-16 w-16 items-center justify-center overflow-hidden rounded-full shadow-2xl backdrop-blur-md border border-current/10">
          <div className="h-8 w-8 animate-pulse rounded-full bg-current opacity-80" />
        </div>
        
        {/* Tiny trailing pulses */}
        <div className="mt-8 flex flex-col items-center gap-3">
          <div className="h-2 w-24 animate-pulse rounded-full bg-current opacity-40" />
          <div className="h-2 w-16 animate-pulse rounded-full bg-current opacity-20" />
        </div>
      </div>
    </div>
  );
}

/**
 * InlineLoader: A subtle multi-dot pulse for inline usages like buttons or small cards.
 * Removes the need for traditional spinning icons.
 */
export function InlineLoader({ className = '' }: { className?: string }) {
  return (
    <div className={`flex items-center justify-center gap-1 ${className}`}>
      <div className="h-1.5 w-1.5 animate-bounce rounded-full bg-current [animation-delay:-0.3s]" />
      <div className="h-1.5 w-1.5 animate-bounce rounded-full bg-current [animation-delay:-0.15s]" />
      <div className="h-1.5 w-1.5 animate-bounce rounded-full bg-current" />
    </div>
  );
}
