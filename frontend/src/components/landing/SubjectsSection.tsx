import { subjects } from "./data";
import { SectionHeading } from "./SectionHeading";

export function SubjectsSection() {
  return (
    <section id="subjects" className="px-4 py-24 md:px-0">
      <div className="mx-auto grid w-[min(1180px,92vw)] gap-12 lg:grid-cols-[0.85fr_1.15fr]">
        <SectionHeading
          eyebrow="فروع الدراسة"
          title="مسارات منظمة جوه واجهة واحدة متماسكة"
          description="ممكن تبدّل المسميات أو المواد أو ترتيب الكروت بسهولة لأن كل الداتا والواجهة متفصلين عن بعض."
          align="start"
        />

        <div className="grid gap-5 sm:grid-cols-2">
          {subjects.map((subject, index) => {
            const Icon = subject.icon;

            return (
              <article
                key={subject.title}
                className={`landing-panel rounded-[30px] p-6 ${
                  index === 1 ? "sm:translate-y-8" : ""
                }`}
              >
                <div className="flex items-center justify-between gap-4">
                  <div className="flex h-14 w-14 items-center justify-center rounded-2xl bg-[var(--landing-card-strong)] text-[var(--landing-accent)]">
                    <Icon className="h-7 w-7" />
                  </div>
                  <span className="text-xs font-black tracking-[0.28em] text-[var(--landing-muted)]">
                    0{index + 1}
                  </span>
                </div>
                <h3 className="mt-5 text-2xl font-black text-[var(--landing-ink)]">
                  {subject.title}
                </h3>
                <p className="mt-3 text-base leading-7 text-[var(--landing-muted)]">
                  {subject.description}
                </p>
              </article>
            );
          })}
        </div>
      </div>
    </section>
  );
}

