import { CtaSection } from "@/components/landing/CtaSection";
import { FeaturesSection } from "@/components/landing/FeaturesSection";
import { HeroSection } from "@/components/landing/HeroSection";
import { LandingFooter } from "@/components/landing/LandingFooter";
import { SubjectsSection } from "@/components/landing/SubjectsSection";
import { TestimonialsSection } from "@/components/landing/TestimonialsSection";

export default function LandingPage() {
  return (
    <main className="landing-page">
      <div className="landing-page__backdrop" />
      <div className="landing-page__texture" />

      <div className="relative z-10 space-y-0">
        <HeroSection />
        <FeaturesSection />
        <SubjectsSection />
        <TestimonialsSection />
        <CtaSection />
        <LandingFooter />
      </div>
    </main>
  );
}
