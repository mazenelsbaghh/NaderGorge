import { notFound } from 'next/navigation';
import HlsPreview from './HlsPreview';

export default function YouTubeHlsPreviewPage() {
  if (process.env.NODE_ENV !== 'development') notFound();
  return <HlsPreview />;
}
