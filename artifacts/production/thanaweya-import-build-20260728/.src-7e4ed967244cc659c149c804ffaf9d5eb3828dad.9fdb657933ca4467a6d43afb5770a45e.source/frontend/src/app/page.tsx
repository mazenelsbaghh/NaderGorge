import { LandingHome } from "@/packages/landing";
import { LiveSupportLauncher } from "@/components/live-support/participant/LiveSupportLauncher";
import { PlatformPopup } from '@/components/platform/PlatformPopup';

export default function LandingPage() {
  return <><LandingHome /><PlatformPopup /><LiveSupportLauncher /></>;
}
