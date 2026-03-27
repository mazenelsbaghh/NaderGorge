import { features } from "./data";
import { SectionHeading } from "./SectionHeading";

export function FeaturesSection() {
  return (
    <section id="features" className="px-4 py-24 md:px-0">
      <div className="mx-auto flex w-[min(1180px,92vw)] flex-col gap-12">
        <SectionHeading
          eyebrow="لماذا هذا الاتجاه؟"
          title="بناء واضح، وتعديل أسهل، وشكل يثبت في الذاكرة"
          description="كل سكشن متعمد يبقى منفصل علشان تقدر تغيّر المحتوى أو الترتيب أو الألوان من مكان واحد من غير ما تلمس الصفحة كلها."
        />

        <div className="grid gap-6 md:grid-cols-2 xl:grid-cols-4">
          {features.map((feature) => {
            const Icon = feature.icon;

            return (
              <article
                key={feature.title}
                className="landing-panel group rounded-[30px] p-7 transition duration-300 hover:-translate-y-1 hover:shadow-[0_20px_50px_rgba(88,55,18,0.14)]"
              >
                <div className="flex h-14 w-14 items-center justify-center rounded-2xl bg-[var(--landing-card-strong)] text-[var(--landing-accent)] transition group-hover:scale-110">
                  <Icon className="h-7 w-7" />
                </div>
                <h3 className="mt-6 text-2xl font-black text-[var(--landing-ink)]">
                  {feature.title}
                </h3>
                <p className="mt-3 text-base leading-7 text-[var(--landing-muted)]">
                  {feature.description}
                </p>
              </article>
            );
          })}
        </div>
      </div>
    </section>
  );
}

