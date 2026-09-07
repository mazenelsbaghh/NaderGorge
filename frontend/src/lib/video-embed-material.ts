import crypto from 'node:crypto';

export type VideoEmbedMaterial = {
  token?: string;
  key?: string;
  Token?: string;
  Key?: string;
};

export function decryptVideoEmbedMaterial(material: VideoEmbedMaterial) {
  const encryptedToken = material.token ?? material.Token;
  const base64Key = material.key ?? material.Key;
  if (!encryptedToken || !base64Key) throw new Error('Missing video material');
  const tokenBytes = Buffer.from(encryptedToken, 'base64');
  if (tokenBytes.length < 28) throw new Error('Invalid video material');
  const decipher = crypto.createDecipheriv('aes-256-gcm', Buffer.from(base64Key, 'base64'), tokenBytes.subarray(0, 12));
  decipher.setAuthTag(tokenBytes.subarray(-16));
  const plaintext = decipher.update(tokenBytes.subarray(12, -16), undefined, 'utf8') + decipher.final('utf8');
  return JSON.parse(plaintext) as { Provider: string; VideoId: string; StudentName?: string; StudentPhone?: string };
}
