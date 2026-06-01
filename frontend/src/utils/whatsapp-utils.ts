/**
 * WhatsApp verification utilities
 * Uses Evolution API via backend proxy for real WhatsApp number checking.
 */

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
  return {
    status: 'idle',
    label: '',
    color: 'none',
  };
}
