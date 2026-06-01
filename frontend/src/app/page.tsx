import { CircularGallerySection } from "@/components/landing/CircularGallerySection";
import { HeroSection } from "@/components/landing/HeroSection";
import { LandingFooter } from "@/components/landing/LandingFooter";
import { TestimonialsSection } from "@/components/landing/TestimonialsSection";

const FALLBACK_REGISTERED_STUDENTS_COUNT = 0;
const LANDING_STUDENTS_BASELINE = 5000;
const API_BASE_URL = process.env.NEXT_PUBLIC_API_URL || "http://localhost:5000/api";

type PlatformStatsResponse = {
  success: boolean;
  data?: {
    registeredStudentsCount: number;
  };
};

async function getRegisteredStudentsCount() {
  try {
    const response = await fetch(`${API_BASE_URL}/public/stats`, {
      next: { revalidate: 300 },
    });

    if (!response.ok) {
      return FALLBACK_REGISTERED_STUDENTS_COUNT;
    }

    const payload = (await response.json()) as PlatformStatsResponse;
    return payload.data?.registeredStudentsCount ?? FALLBACK_REGISTERED_STUDENTS_COUNT;
  } catch {
    return FALLBACK_REGISTERED_STUDENTS_COUNT;
  }
}

export default async function LandingPage() {
  const registeredStudentsCount = await getRegisteredStudentsCount();

  return (
    <main className="landing-page">
      <div className="relative z-10 space-y-0">
        <HeroSection
          registeredStudentsCount={registeredStudentsCount}
          baselineStudentsCount={LANDING_STUDENTS_BASELINE}
        />
        <CircularGallerySection />
        <TestimonialsSection />
        <LandingFooter />
      </div>
    </main>
  );
}
