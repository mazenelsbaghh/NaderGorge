import { notFound } from 'next/navigation';
import VcdnPreview from './VcdnPreview';

export default function VcdnPreviewPage() {
  if (process.env.NODE_ENV !== 'development') notFound();
  return <VcdnPreview />;
}
