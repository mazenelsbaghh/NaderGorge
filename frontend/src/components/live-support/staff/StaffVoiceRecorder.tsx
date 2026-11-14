'use client';

import { useEffect, useRef, useState } from 'react';
import { LoaderCircle, Mic, Send, Square, Trash2 } from 'lucide-react';
import { createLiveSupportRecording } from '@/lib/live-support-recording';

interface StaffVoiceRecorderProps {
  disabled?: boolean;
  uploading?: boolean;
  onSend: (file: File) => Promise<boolean>;
}

const MAX_RECORDING_SECONDS = 120;

export function StaffVoiceRecorder({ disabled = false, uploading = false, onSend }: StaffVoiceRecorderProps) {
  const [supported, setSupported] = useState<boolean | null>(null);
  const [recording, setRecording] = useState(false);
  const [recordingSeconds, setRecordingSeconds] = useState(0);
  const [recordedFile, setRecordedFile] = useState<File>();
  const [previewUrl, setPreviewUrl] = useState<string>();
  const [sending, setSending] = useState(false);
  const [error, setError] = useState('');
  const recorderRef = useRef<MediaRecorder | null>(null);
  const streamRef = useRef<MediaStream | null>(null);
  const micRequestPendingRef = useRef(false);
  const discardOnStopRef = useRef(false);

  useEffect(() => {
    setSupported(typeof window !== 'undefined' && 'MediaRecorder' in window && Boolean(navigator.mediaDevices?.getUserMedia));
  }, []);

  useEffect(() => {
    if (!recording) return;
    const timer = window.setInterval(() => {
      setRecordingSeconds((seconds) => {
        const next = seconds + 1;
        if (next >= MAX_RECORDING_SECONDS) window.setTimeout(stopRecording, 0);
        return next;
      });
    }, 1000);
    return () => window.clearInterval(timer);
  }, [recording]);

  useEffect(() => () => {
    discardOnStopRef.current = true;
    stopTracks();
  }, []);

  useEffect(() => () => {
    if (previewUrl) URL.revokeObjectURL(previewUrl);
  }, [previewUrl]);

  function stopTracks() {
    streamRef.current?.getTracks().forEach((track) => track.stop());
    streamRef.current = null;
  }

  function clearRecorded() {
    setRecordedFile(undefined);
    setPreviewUrl(undefined);
    setRecordingSeconds(0);
  }

  async function startRecording() {
    if (disabled || uploading || sending || recorderRef.current || micRequestPendingRef.current) return;
    if (!supported) {
      setError('المتصفح الحالي لا يدعم تسجيل الصوت. استخدم Chrome أو Safari محدثًا.');
      return;
    }

    setError('');
    clearRecorded();
    micRequestPendingRef.current = true;
    discardOnStopRef.current = false;
    try {
      const stream = await navigator.mediaDevices.getUserMedia({ audio: true });
      streamRef.current = stream;
      if (discardOnStopRef.current) {
        stopTracks();
        return;
      }
      const mimeType = getSupportedMimeType();
      const recorder = mimeType ? new MediaRecorder(stream, { mimeType }) : new MediaRecorder(stream);
      const chunks: Blob[] = [];
      recorderRef.current = recorder;
      recorder.ondataavailable = (event) => {
        if (event.data.size > 0) chunks.push(event.data);
      };
      recorder.onstop = async () => {
        stopTracks();
        try {
          if (discardOnStopRef.current) return;
          if (chunks.length === 0) {
            setError('لم يتم التقاط صوت. حاول التسجيل مرة أخرى.');
            return;
          }
          const file = await createLiveSupportRecording(chunks);
          if (discardOnStopRef.current) return;
          if (!file) {
            setError('تعذر تجهيز التسجيل الصوتي. حاول التسجيل مرة أخرى.');
            return;
          }
          setRecordedFile(file);
          setPreviewUrl(URL.createObjectURL(file));
        } catch {
          if (!discardOnStopRef.current) setError('تعذر قراءة التسجيل الصوتي. حاول التسجيل مرة أخرى.');
        } finally {
          recorderRef.current = null;
          setRecording(false);
        }
      };
      recorder.onerror = () => {
        discardOnStopRef.current = true;
        setError('حدث خطأ أثناء التسجيل. حاول مرة أخرى.');
      };
      recorder.start();
      setRecordingSeconds(0);
      setRecording(true);
    } catch {
      recorderRef.current = null;
      stopTracks();
      if (!discardOnStopRef.current) setError('تعذر بدء التسجيل. تحقق من إذن الميكروفون وحاول مرة أخرى.');
    } finally {
      micRequestPendingRef.current = false;
    }
  }

  function stopRecording() {
    if (!recorderRef.current || recorderRef.current.state === 'inactive') return;
    recorderRef.current.stop();
  }

  function cancelRecording() {
    discardOnStopRef.current = true;
    if (recorderRef.current && recorderRef.current.state !== 'inactive') recorderRef.current.stop();
    stopTracks();
    clearRecorded();
    setError('');
  }

  async function sendRecording() {
    if (!recordedFile || sending || uploading || disabled) return;
    setSending(true);
    setError('');
    try {
      if (await onSend(recordedFile)) clearRecorded();
    } catch {
      setError('تعذر إرسال التسجيل. يمكنك المحاولة مرة أخرى.');
    } finally {
      setSending(false);
    }
  }

  if (supported === false) return <p className="mt-2 text-xs text-[var(--admin-muted)]">تسجيل الصوت غير مدعوم في هذا المتصفح.</p>;

  return <div className="mt-3 space-y-2" aria-label="تسجيل صوتي للموظف">
    {recording ? <div className="flex flex-wrap items-center gap-2 rounded-xl border border-red-200 bg-red-50 p-2 text-red-800" role="status" aria-live="polite">
      <span className="inline-flex items-center gap-2 text-sm font-bold"><span className="size-2 animate-pulse rounded-full bg-red-600" />جارٍ التسجيل · {formatDuration(recordingSeconds)}</span>
      <button type="button" onClick={stopRecording} className="mr-auto inline-flex min-h-10 items-center gap-2 rounded-lg bg-red-600 px-3 text-sm font-bold text-white hover:bg-red-700"><Square size={15} fill="currentColor" />إنهاء التسجيل</button>
      <button type="button" onClick={cancelRecording} className="inline-flex min-h-10 items-center gap-2 rounded-lg border border-red-200 px-3 text-sm font-bold hover:bg-white"><Trash2 size={15} />إلغاء</button>
    </div> : recordedFile && previewUrl ? <div className="flex flex-wrap items-center gap-2 rounded-xl border border-[var(--admin-border)] bg-[var(--admin-card-soft)] p-2">
      <audio controls preload="metadata" src={previewUrl} className="h-10 min-w-0 flex-1" aria-label="معاينة التسجيل الصوتي" />
      <span className="text-xs text-[var(--admin-muted)]">{formatDuration(recordingSeconds)}</span>
      <button type="button" disabled={sending || uploading || disabled} onClick={() => void sendRecording()} className="inline-flex min-h-10 items-center gap-2 rounded-lg bg-[var(--admin-primary)] px-3 text-sm font-bold text-[var(--admin-primary-contrast)] disabled:opacity-50">{sending || uploading ? <LoaderCircle size={15} className="animate-spin" /> : <Send size={15} />}إرسال التسجيل</button>
      <button type="button" disabled={sending || uploading} onClick={cancelRecording} className="inline-flex min-h-10 items-center gap-2 rounded-lg border border-[var(--admin-border)] px-3 text-sm font-bold text-[var(--admin-text)] hover:bg-[var(--admin-hover)] disabled:opacity-50"><Trash2 size={15} />حذف</button>
    </div> : <button type="button" disabled={disabled || uploading || sending || supported === null} onClick={() => void startRecording()} className="inline-flex min-h-10 items-center gap-2 rounded-xl border border-[var(--admin-border)] bg-[var(--admin-card-soft)] px-3 text-sm font-bold text-[var(--admin-primary)] hover:bg-[var(--admin-hover)] disabled:opacity-50"><Mic size={17} />تسجيل صوتي للطالب</button>}
    {error ? <p role="alert" className="text-xs font-medium text-[var(--admin-danger)]">{error}</p> : null}
  </div>;
}

function getSupportedMimeType() {
  if (typeof MediaRecorder === 'undefined' || typeof MediaRecorder.isTypeSupported !== 'function') return '';
  return ['audio/webm;codecs=opus', 'audio/webm', 'audio/ogg;codecs=opus', 'audio/mp4;codecs=mp4a.40.2', 'audio/mp4']
    .find((mimeType) => MediaRecorder.isTypeSupported(mimeType)) ?? '';
}

function formatDuration(seconds: number) {
  const minutes = Math.floor(seconds / 60).toString().padStart(2, '0');
  const remainder = (seconds % 60).toString().padStart(2, '0');
  return `${minutes}:${remainder}`;
}
