import { notFound } from 'next/navigation';
import QualityPreview from './QualityPreview';

export default function YouTubeQualityPreviewPage() {
  if (process.env.NODE_ENV !== 'development') notFound();
  return <QualityPreview lessonTitle={process.env.MASSAR_LOCAL_YOUTUBE_PREVIEW_TITLE} />;
}
