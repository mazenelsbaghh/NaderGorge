'use client';

import { useState } from 'react';
import { Copy, CheckCircle2 } from 'lucide-react';

interface CopyParentLinkButtonProps {
  userId: string;
}

export function CopyParentLinkButton({ userId }: CopyParentLinkButtonProps) {
  const [copied, setCopied] = useState(false);

  const copyToClipboard = async () => {
    const parentLink = `${window.location.origin}/parent-report/${userId}`;
    try {
      await navigator.clipboard.writeText(parentLink);
      setCopied(true);
      setTimeout(() => setCopied(false), 2000);
    } catch (e) {
      console.error('Failed to copy', e);
    }
  };

  return (
    <button
      onClick={copyToClipboard}
      className={`inline-flex items-center text-sm font-medium transition-colors ${
        copied 
          ? 'text-[var(--admin-primary)]' 
          : 'text-[var(--admin-muted)] hover:text-[var(--admin-text)]'
      }`}
      title="Copy Parent Link"
    >
      {copied ? (
        <CheckCircle2 className="w-4 h-4 mr-1" />
      ) : (
        <Copy className="w-4 h-4 mr-1" />
      )}
      <span>Parent Link</span>
    </button>
  );
}
