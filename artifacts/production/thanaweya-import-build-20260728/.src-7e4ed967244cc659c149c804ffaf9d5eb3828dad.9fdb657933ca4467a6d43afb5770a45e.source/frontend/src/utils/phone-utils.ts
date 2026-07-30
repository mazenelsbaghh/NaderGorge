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
