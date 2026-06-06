'use client';

import { devConsole } from '@/utils/dev-console';
/**
 * QrScanner — Component to scan QR codes
 * Uses @yudiel/react-qr-scanner for device camera integration.
 */

import { useState } from 'react';
import { Scanner } from '@yudiel/react-qr-scanner';
import { Camera, X } from 'lucide-react';
import { useRouter } from 'next/navigation';

export function QrScanner() {
  const router = useRouter();
  const [isOpen, setIsOpen] = useState(false);
  const [scanning, setScanning] = useState(false);

  const handleScan = (result: any) => {
    if (!result || !result.length) return;
    
    // Get the scanned text
    const text = result[0].rawValue || result[0].text || result[0];
    
    if (text) {
      setScanning(true);
      setIsOpen(false);
      
      // Navigate to the scanned URL (assuming it's a relative path starting with /api/qr/ or full URL)
      try {
        const url = new URL(text);
        if (url.pathname.startsWith('/api/qr/')) {
          router.push(url.pathname);
        } else {
          // If it's just the code string, we can try pushing directly
          router.push(`/api/qr/${encodeURIComponent(text)}`);
        }
      } catch {
        // Not a valid URL, assume it's just the raw code
        router.push(`/api/qr/${encodeURIComponent(text)}`);
      }
    }
  };

  const handleError = (error: unknown) => {
    devConsole.warn('QR Scan Error:', error);
  };

  if (!isOpen) {
    return (
      <button
        onClick={() => setIsOpen(true)}
        className="auth-btn-primary flex items-center justify-center gap-2"
        disabled={scanning}
      >
        <Camera size={20} />
        <span>{scanning ? 'جاري المعالجة...' : 'مسح كود QR'}</span>
      </button>
    );
  }

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/80 backdrop-blur-sm p-4">
      <div className="bg-[var(--admin-card)] rounded-2xl p-4 w-full max-w-sm relative shadow-2xl border border-[var(--admin-border)]">
        
        {/* Header */}
        <div className="flex items-center justify-between mb-4">
          <h3 className="font-bold text-lg text-[var(--admin-text)]">وجّه الكاميرا للكود</h3>
          <button 
            onClick={() => setIsOpen(false)}
            className="p-2 rounded-full hover:bg-[var(--admin-hover)] text-[var(--admin-muted)] transition-colors"
          >
            <X size={20} />
          </button>
        </div>
        
        {/* Scanner Viewport */}
        <div className="rounded-xl overflow-hidden bg-black/50 aspect-square relative border border-[var(--admin-border)]">
          <Scanner
            onScan={handleScan}
            onError={handleError}
            formats={['qr_code']}
            styles={{ container: { width: '100%', height: '100%' } }}
            components={{
              zoom: true,
              finder: true,
            }}
          />
        </div>
        
        {/* Hint */}
        <p className="text-center text-sm text-[var(--admin-muted)] mt-4">
          سيتم تفعيل الكود تلقائياً بمجرد مسحه
        </p>
      </div>
    </div>
  );
}
