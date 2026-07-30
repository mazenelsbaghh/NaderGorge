import { GlobalNav } from '@/components/layout/GlobalNav';
import { LiveSupportLauncher } from '@/components/live-support/participant/LiveSupportLauncher';

export default function PublicLayout({ children }: { children: React.ReactNode }) {
  return (
    <>
      <GlobalNav />
      {children}
      <LiveSupportLauncher />
    </>
  );
}
