import type { Metadata } from 'next';
import { ThanaweyaResultsClient } from './ThanaweyaResultsClient';

export const metadata: Metadata = {
  title: 'نتيجة الثانوية العامة 2026 | منصة مسار',
  description: 'استعلم عن نتيجة الثانوية العامة برقم الجلوس على منصة مسار.',
};

export default function ThanaweyaResultsPage() {
  return <ThanaweyaResultsClient />;
}
