'use client';

/**
 * QrDisplay — Renders printable QR codes for a given set of plaintext codes.
 *
 * Uses `qrcode.react` to generate SVG QR codes.
 * Provides a Print button that uses a CSS @media print layout.
 */

import { QRCodeSVG } from 'qrcode.react';
import { Printer } from 'lucide-react';
import { useRef } from 'react';
import { getSurfaceOrigins } from '@/packages/surface-runtime/config';

interface QrDisplayProps {
  codes: string[];
  groupName?: string;
  baseUrl?: string;
}

export function QrDisplay({ codes, groupName = 'Batch', baseUrl }: QrDisplayProps) {
  const printRef = useRef<HTMLDivElement>(null);
  const origins = getSurfaceOrigins();
  const effectiveBaseUrl = baseUrl || origins.student;
  const isLocalhost = effectiveBaseUrl.includes('localhost') || effectiveBaseUrl.includes('0.0.0.0') || effectiveBaseUrl.includes('127.0.0.1');

  const handlePrint = () => {
    window.print();
  };

  if (!codes || codes.length === 0) return null;

  return (
    <div className="space-y-4">
      {/* ── Toolbar ── */}
      <div className="flex items-center justify-between print:hidden bg-[var(--admin-card-soft)] p-4 rounded-xl border border-[var(--admin-border)]">
        <div>
          <h3 className="font-bold text-[var(--admin-text)]">طباعة رموز QR</h3>
          <p className="text-sm text-[var(--admin-muted)]">عدد الأكواد للطباعة: {codes.length}</p>
        </div>
        <button
          onClick={handlePrint}
          className="flex items-center gap-2 px-4 py-2 bg-[var(--admin-primary)] text-white rounded-lg hover:opacity-90 transition-opacity font-bold shadow-lg"
        >
          <Printer size={18} />
          <span>اطبع الكروت</span>
        </button>
      </div>

      {isLocalhost && (
        <div className="p-4 bg-yellow-500/10 border border-yellow-500/30 text-yellow-500 rounded-xl text-sm font-bold flex flex-col gap-1 print:hidden">
          <span className="text-base flex items-center gap-1.5 font-bold">⚠️ تنبيه: عنوان الرابط الحالي محلي ({effectiveBaseUrl})</span>
          <span className="font-normal text-xs opacity-90 leading-relaxed">
            لن يتمكن الطلاب من مسح رمز QR بنجاح عبر هواتفهم لأنه يشير إلى خادم محلي. يرجى التأكد من ضبط متغير البيئة <code className="px-1.5 py-0.5 bg-yellow-500/20 rounded font-mono">NEXT_PUBLIC_APP_URL</code> بالرابط الفعلي للمنصة.
          </span>
        </div>
      )}

      {/* ── Printable Area ── */}
      {/* 
        In screen mode, displays a scrollable grid. 
        In print mode, forces an A4 grid without scrollbars.
      */}
      <div className="max-h-[600px] overflow-y-auto print:max-h-none print:overflow-visible">
        <div
          ref={printRef}
          className="grid grid-cols-2 sm:grid-cols-3 md:grid-cols-4 lg:grid-cols-5 gap-4 print:grid-cols-5 print:gap-4 print:m-0"
        >
          {codes.map((code) => {
            const qrUrl = `${effectiveBaseUrl}/api/qr/${code}`;
            return (
              <div
                key={code}
                className="flex flex-col items-center justify-center p-4 bg-white border-2 border-dashed border-gray-300 rounded-xl print:border-solid print:border-black print:rounded-none print:break-inside-avoid print:p-2"
                style={{ aspectRatio: '1/1.2' }}
              >
                {/* Brand / Title per card */}
                <div className="text-black font-bold text-xs sm:text-xs mb-2 text-center uppercase tracking-widest print:text-xs">
                  MASSAR PLATFORM
                </div>

                <QRCodeSVG
                  value={qrUrl}
                  size={120}
                  level="M"
                  includeMargin={false}
                  className="w-full h-auto max-w-[120px] print:w-[3cm] print:h-[3cm]"
                />
                
                {/* Code text below QR */}
                <div className="mt-3 text-center">
                  <div className="font-mono font-bold text-gray-900 text-sm sm:text-base print:text-sm tracking-widest">
                    {code}
                  </div>
                  <div className="text-gray-500 text-xs print:text-xs mt-1">
                    {groupName}
                  </div>
                </div>
              </div>
            );
          })}
        </div>
      </div>

      {/* ── Hidden Print Styles injected globally for this component ── */}
      <style dangerouslySetInnerHTML={{ __html: `
        @media print {
          body * {
            visibility: hidden;
          }
          .admin-sidebar, .admin-navbar { display: none !important; }
          .print\\:hidden { display: none !important; }
          
          /* Only show the grid contents */
          .grid.print\\:grid-cols-5, .grid.print\\:grid-cols-5 * {
            visibility: visible;
          }
          .grid.print\\:grid-cols-5 {
            position: absolute;
            left: 0;
            top: 0;
            width: 100%;
          }
        }
      `}} />
    </div>
  );
}
