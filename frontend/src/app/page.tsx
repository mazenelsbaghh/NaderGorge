import { LandingHome } from "@/packages/landing";
import { LiveSupportLauncher } from "@/components/live-support/participant/LiveSupportLauncher";

export default async function LandingPage() {
  return <><LandingHome /><LiveSupportLauncher /></>;
}
