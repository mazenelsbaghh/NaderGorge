import type { Metadata } from 'next';

export const metadata: Metadata = {
  title: 'About Masar Platform',
  description: 'Learn about Masar Platform learning philosophy, track record, and academic excellence.',
};

export default function AboutLayout({ children }: { children: React.ReactNode }) {
  return <>{children}</>;
}
