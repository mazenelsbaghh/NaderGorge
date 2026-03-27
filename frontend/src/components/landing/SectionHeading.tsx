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

  return (
    <div className={`flex max-w-2xl flex-col gap-4 ${alignment}`}>
      <span className="landing-chip">{eyebrow}</span>
      <div className="space-y-3">
        <h2 className="text-3xl font-black tracking-tight text-[var(--landing-ink)] md:text-5xl">
          {title}
        </h2>
        <p className="text-base leading-8 text-[var(--landing-muted)] md:text-lg">
          {description}
        </p>
      </div>
    </div>
  );
}

