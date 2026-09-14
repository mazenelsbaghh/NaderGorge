import type { Metadata } from 'next';

export const metadata: Metadata = {
  title: 'About Massar Academy',
  description: 'Learn about Massar Academy learning philosophy, track record, and academic excellence.',
};

export default function AboutLayout({ children }: { children: React.ReactNode }) {
  return <>{children}</>;
}
