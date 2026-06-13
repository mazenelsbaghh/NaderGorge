type ProgressRingProps = {
  percent: number;
  sizeClass?: string;
};

const RADIUS = 18;
const CIRCUMFERENCE = 2 * Math.PI * RADIUS;

export function ProgressRing({ percent, sizeClass = "h-16 w-16" }: ProgressRingProps) {
  const normalizedPercent = Math.max(0, Math.min(Math.round(percent), 100));
  const dashOffset = CIRCUMFERENCE * (1 - normalizedPercent / 100);

  return (
    <div
      className={`relative shrink-0 ${sizeClass}`}
      role="progressbar"
      aria-label="التقدم الكلي"
      aria-valuemin={0}
      aria-valuemax={100}
      aria-valuenow={normalizedPercent}
    >
      <svg viewBox="0 0 44 44" className="h-full w-full -rotate-90" aria-hidden="true">
        <circle
          cx="22"
          cy="22"
          r={RADIUS}
          fill="none"
          stroke="var(--admin-card-strong)"
          strokeWidth="4"
        />
        <circle
          cx="22"
          cy="22"
          r={RADIUS}
          fill="none"
          stroke="var(--admin-primary)"
          strokeWidth="4"
          strokeLinecap="round"
          strokeDasharray={CIRCUMFERENCE}
          strokeDashoffset={dashOffset}
        />
      </svg>
      <span className="absolute inset-0 flex items-center justify-center text-xs font-black text-[var(--admin-text)]">
        {normalizedPercent}%
      </span>
    </div>
  );
}
