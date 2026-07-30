'use client';

import { useRouter } from 'next/navigation';
import { ArrowLeft } from 'lucide-react';
import NeumorphButton from '@/components/ui/neumorph-button';

export function AdminBackButton({ label = "العودة" }: { label?: string }) {
  const router = useRouter();
  return (
    <NeumorphButton
      intent="ghost"
      size="sm"
      onClick={() => router.back()}
    >
      <ArrowLeft className="h-4 w-4" />
      {label}
    </NeumorphButton>
  );
}
