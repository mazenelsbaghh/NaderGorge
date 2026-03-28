import type { Metadata } from 'next';

export const metadata: Metadata = {
  title: 'About Mr. Nader George | Nader George Academy',
  description: 'Learn about Mr. Nader George\'s teaching philosophy, track record, and decade of academic excellence.',
};

export default function AboutLayout({ children }: { children: React.ReactNode }) {
  return <>{children}</>;
}
