import type { Metadata } from 'next';

export const metadata: Metadata = {
  title: 'About Massar Platform',
  description: 'Learn about Massar Platform learning philosophy, track record, and academic excellence.',
};

export default function AboutLayout({ children }: { children: React.ReactNode }) {
  return <>{children}</>;
}
