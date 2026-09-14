export function normalizeEgyptianMobileInput(phone: string): string {
  const cleaned = phone
    .replace(/[\u0660-\u0669]/g, digit => String(digit.charCodeAt(0) - 0x0660))
    .replace(/[\u06f0-\u06f9]/g, digit => String(digit.charCodeAt(0) - 0x06f0))
    // Preserve invalid letters and extra digits so validation cannot silently change the recipient.
    .replace(/[\s()\-\u061c\u200b-\u200f\u202a-\u202e\u2066-\u2069\ufeff]/g, '');
  return cleaned.replace(/^(?:\+20|0020|20)(1[0125]\d{8})$/, '0$1');
}

/**
 * Normalizes Egyptian mobile phone numbers to E.164 format (starting with 20).
 * E.g., "01012345678" -> "201012345678"
 * E.g., "+20 10 1234 5678" -> "201012345678"
 */
export function normalizeEgyptPhoneNumber(phone: string): string {
  let cleaned = phone.replace(/\D/g, ""); // remove non-digits
  
  // If it starts with local "0", convert it to "20"
  if (cleaned.startsWith("0")) {
    cleaned = "2" + cleaned;
  } 
  // If it does not start with Egypt country code "20" but starts with mobile prefixes like "10", "11", "12", "15"
  else if (!cleaned.startsWith("20") && /^(10|11|12|15)/.test(cleaned)) {
    cleaned = "20" + cleaned;
  }
  
  return cleaned;
}

/**
 * Generates a pre-filled WhatsApp click-to-chat web redirect link.
 */
export function getWhatsAppLink(phone: string, text: string): string {
  const normalized = normalizeEgyptPhoneNumber(phone);
  return `https://wa.me/${normalized}?text=${encodeURIComponent(text)}`;
}
