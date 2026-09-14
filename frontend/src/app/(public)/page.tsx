import { LandingHome } from "@/packages/landing";
import { PlatformPopup } from '@/components/platform/PlatformPopup';

// The proof number must be rendered from the live internal API.  Do not bake a
// fallback count into the production image when the build environment cannot
// reach the API.
export const dynamic = 'force-dynamic';

export default function LandingPage() {
  return <><LandingHome /><PlatformPopup /></>;
}
