import { CircularGallerySection } from '@/components/landing/CircularGallerySection';
import { HeroSection } from '@/components/landing/HeroSection';
import { LandingFooter } from '@/components/landing/LandingFooter';
import { TestimonialsSection } from '@/components/landing/TestimonialsSection';
import { ScholarlyParticles } from '@/components/landing/ScholarlyParticles';

const FALLBACK_REGISTERED_STUDENTS_COUNT = 0;
const LANDING_STUDENTS_BASELINE = 5000;

const API_BASE_URL =
  process.env.INTERNAL_API_URL ||
  process.env.NEXT_PUBLIC_API_URL ||
  'http://localhost:5245/api';

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

export async function LandingHome() {
  const registeredStudentsCount = await getRegisteredStudentsCount();

  return (
    <main className="landing-page relative min-h-screen overflow-hidden">
      <ScholarlyParticles />
      <div className="landing-shell relative z-10">
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

