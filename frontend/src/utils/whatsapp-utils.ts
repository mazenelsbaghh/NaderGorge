/**
 * WhatsApp verification utilities
 * Uses Evolution API via backend proxy for real WhatsApp number checking.
 */
import { useState, useEffect, useRef } from 'react';
import { checkWhatsApp } from '@/services/whatsapp-service';

/* ────────────────────────────────────────────────────────────────── */
/*  Types                                                           */
/* ────────────────────────────────────────────────────────────────── */

export type WhatsAppStatus = 'idle' | 'checking' | 'verified' | 'not_found' | 'error';

export interface WhatsAppCheckState {
  status: WhatsAppStatus;
  label: string;
  color: 'green' | 'red' | 'gray' | 'amber' | 'none';
}

/* ────────────────────────────────────────────────────────────────── */
/*  Format helpers                                                  */
/* ────────────────────────────────────────────────────────────────── */

/** Check if a phone number looks like a valid Egyptian mobile number */
export function isValidEgyptianPhone(phone: string): boolean {
  const cleaned = phone.replace(/\s|-/g, '');
  return /^01[0-9]{9}$/.test(cleaned);
}

/* ────────────────────────────────────────────────────────────────── */
/*  React hook — useWhatsAppCheck                                   */
/* ────────────────────────────────────────────────────────────────── */

/**
 * Auto-checks a phone number against WhatsApp after a debounce period.
 * Returns the current verification state (status + label + color).
 *
 * @param phone - The raw phone input from the form field
 * @param debounceMs - Debounce delay in milliseconds (default: 1000)
 */
export function useWhatsAppCheck(phone: string, debounceMs = 1000): WhatsAppCheckState {
  const [state, setState] = useState<WhatsAppCheckState>({
    status: 'idle',
    label: '',
    color: 'none',
  });
  const lastChecked = useRef('');

  useEffect(() => {
    const cleaned = phone?.replace(/\s|-/g, '') ?? '';

    // Reset if phone cleared or too short
    if (!cleaned || cleaned.length < 11) {
      setState({ status: 'idle', label: '', color: 'none' });
      lastChecked.current = '';
      return;
    }

    // Don't re-check same number
    if (cleaned === lastChecked.current) return;

    // Must be a valid Egyptian number
    if (!isValidEgyptianPhone(cleaned)) {
      setState({
        status: 'error',
        label: 'يرجى إدخال رقم مصري صحيح (يبدأ بـ 01)',
        color: 'red',
      });
      return;
    }

    // Start debounce
    setState({ status: 'checking', label: 'جارٍ التحقق من واتساب...', color: 'amber' });

    const timer = setTimeout(async () => {
      try {
        const result = await checkWhatsApp(cleaned);
        lastChecked.current = cleaned;

        if (result.exists === true) {
          setState({
            status: 'verified',
            label: '✓ الرقم مسجل على واتساب',
            color: 'green',
          });
        } else if (result.exists === false) {
          setState({
            status: 'not_found',
            label: '✗ الرقم ليس على واتساب',
            color: 'red',
          });
        } else {
          // exists === null → service unavailable
          setState({
            status: 'error',
            label: 'تعذر التحقق من واتساب',
            color: 'gray',
          });
        }
      } catch {
        lastChecked.current = '';
        setState({
          status: 'error',
          label: 'تعذر التحقق من واتساب',
          color: 'gray',
        });
      }
    }, debounceMs);

    return () => clearTimeout(timer);
  }, [phone, debounceMs]);

  return state;
}
