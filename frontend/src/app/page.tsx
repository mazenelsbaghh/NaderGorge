import { CircularGallerySection } from "@/components/landing/CircularGallerySection";
import { HeroSection } from "@/components/landing/HeroSection";
import { LandingFooter } from "@/components/landing/LandingFooter";
import { TestimonialsSection } from "@/components/landing/TestimonialsSection";

export default function LandingPage() {
  return (
    <main className="landing-page">
      <div className="relative z-10 space-y-0">
        <HeroSection />
        <CircularGallerySection />
        <TestimonialsSection />
        <LandingFooter />
      </div>
    </main>
  );
}
