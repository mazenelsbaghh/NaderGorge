import { GlobalNav } from '@/components/layout/GlobalNav';
import { DeferredLiveSupportLauncher } from '@/components/live-support/participant/DeferredLiveSupportLauncher';

export default function PublicLayout({ children }: { children: React.ReactNode }) {
  return (
    <>
      <GlobalNav />
      {children}
      <DeferredLiveSupportLauncher />
    </>
  );
}
