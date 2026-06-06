import { devConsole } from '@/utils/dev-console';
export const decryptVideoId = async (encryptedToken: string, base64Key: string): Promise<{ provider: string; videoId: string }> => {
  try {
    // 1. Decode base64 inputs
    const keyBytes = Uint8Array.from(atob(base64Key), c => c.charCodeAt(0));
    const tokenBytes = Uint8Array.from(atob(encryptedToken), c => c.charCodeAt(0));

    // 2. Extract IV (12 bytes), CipherText, and Tag (16 bytes)
    // In our backend, the token is: IV (12) + CipherText (N) + Tag (16)
    const IV_SIZE = 12;
    const TAG_SIZE = 16;
    
    if (tokenBytes.length < IV_SIZE + TAG_SIZE) {
      throw new Error("Invalid token length");
    }

    const iv = tokenBytes.slice(0, IV_SIZE);
    // Web Crypto API AES-GCM decrypt expects CipherText + Tag combined as the data
    const dataAndTag = tokenBytes.slice(IV_SIZE);

    // 3. Import the AES-GCM key
    const cryptoKey = await window.crypto.subtle.importKey(
      "raw",
      keyBytes,
      {
        name: "AES-GCM",
      },
      false,
      ["decrypt"]
    );

    // 4. Decrypt
    const decryptedBuffer = await window.crypto.subtle.decrypt(
      {
        name: "AES-GCM",
        iv: iv,
      },
      cryptoKey,
      dataAndTag
    );

    // 5. Parse the JSON result
    const decoder = new TextDecoder();
    const plainText = decoder.decode(decryptedBuffer);
    
    const parsed = JSON.parse(plainText) as { Provider: string; VideoId: string };
    
    return {
      provider: parsed.Provider,
      videoId: parsed.VideoId
    };
  } catch (err) {
    devConsole.error("Failed to decrypt video session", err);
    throw new Error("Video decryption failed. Session might be invalid or expired.");
  }
};
