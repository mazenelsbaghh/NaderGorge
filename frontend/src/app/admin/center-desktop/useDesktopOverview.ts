'use client';

import { useCallback, useEffect, useRef, useState } from 'react';
import {
  getDesktopStatus, getDesktopUploads,
  type DesktopReceipt, type DesktopStatus,
} from '@/services/center-desktop-service';

export function useDesktopOverview() {
  const [status, setStatus] = useState<DesktopStatus | null>(null);
  const [uploads, setUploads] = useState<DesktopReceipt[]>([]);
  const [cursor, setCursor] = useState('');
  const [loading, setLoading] = useState(true);
  const [loadingMore, setLoadingMore] = useState(false);
  const [error, setError] = useState('');
  const [uploadError, setUploadError] = useState('');
  const [updatedAt, setUpdatedAt] = useState<string | null>(null);
  const active = useRef<AbortController | null>(null);
  const more = useRef<AbortController | null>(null);

  const reload = useCallback(async () => {
    active.current?.abort();
    more.current?.abort();
    const request = new AbortController();
    active.current = request;
    setLoading(true); setLoadingMore(false); setError('');
    setUploadError('');
    try {
      const nextStatus = await getDesktopStatus(request.signal);
      if (request.signal.aborted) return;
      setStatus(nextStatus);
      if (!nextStatus.available) {
        setUploads([]); setCursor(''); setUpdatedAt(null);
        return;
      }
      try {
        const page = await getDesktopUploads(request.signal);
        if (request.signal.aborted) return;
        setUploads(page.uploads); setCursor(page.nextCursor);
        setUpdatedAt(new Date().toISOString());
      } catch {
        if (!request.signal.aborted) setUploadError('تعذر تحديث النسخ. أي بيانات ظاهرة هي آخر نتيجة تم تحميلها؛ حاول مرة أخرى.');
      }
    } catch {
      if (!request.signal.aborted) setError('تعذر الاتصال بخدمة برنامج السنتر. البيانات الظاهرة قديمة إن وجدت؛ جرّب التحديث.');
    } finally {
      if (!request.signal.aborted) setLoading(false);
    }
  }, []);

  useEffect(() => {
    void reload();
    return () => { active.current?.abort(); more.current?.abort(); };
  }, [reload]);

  const loadMore = async () => {
    if (!cursor || loading || more.current) return;
    const request = new AbortController();
    more.current = request;
    setLoadingMore(true); setUploadError('');
    try {
      const page = await getDesktopUploads(request.signal, cursor);
      if (request.signal.aborted) return;
      setUploads(previous => [...new Map([...previous, ...page.uploads].map(item => [item.uploadId, item])).values()]);
      setCursor(page.nextCursor);
    } catch {
      if (!request.signal.aborted) setUploadError('تعذر تحميل المزيد. النسخ المحمّلة ما زالت متاحة؛ حاول مرة أخرى.');
    } finally {
      if (more.current === request) more.current = null;
      if (!request.signal.aborted) setLoadingMore(false);
    }
  };
  return { status, uploads, cursor, loading, loadingMore, error, uploadError, updatedAt, reload, loadMore };
}
