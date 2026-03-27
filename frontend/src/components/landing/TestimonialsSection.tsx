import { Quote } from "lucide-react";

import { testimonials } from "./data";
import { SectionHeading } from "./SectionHeading";

export function TestimonialsSection() {
  return (
    <section id="testimonials" className="px-4 py-24 md:px-0">
      <div className="mx-auto flex w-[min(1180px,92vw)] flex-col gap-12">
        <SectionHeading
          eyebrow="انطباع الزوار"
          title="الصوت العام دافي، راقٍ، وسهل يثق فيه الطالب وولي الأمر"
          description="المقصود هنا إن الصفحة توصل هوية الأستاذ بسرعة: متميزة بصريًا لكن واضحة في الرسالة والـ call to action."
        />

        <div className="grid gap-6 lg:grid-cols-3">
          {testimonials.map((item) => (
            <article key={item.name} className="landing-panel rounded-[32px] p-7">
              <div className="flex items-center justify-between">
                <div className="flex h-14 w-14 items-center justify-center rounded-2xl bg-[var(--landing-card-strong)] text-[var(--landing-accent)]">
                  <Quote className="h-6 w-6" />
                </div>
                <div className="text-left">
                  <p className="text-lg font-black text-[var(--landing-ink)]">{item.name}</p>
                  <p className="text-sm font-semibold text-[var(--landing-muted)]">{item.role}</p>
                </div>
              </div>
              <p className="mt-6 text-base leading-8 text-[var(--landing-muted)]">
                &ldquo;{item.quote}&rdquo;
              </p>
            </article>
          ))}
        </div>
      </div>
    </section>
  );
}

