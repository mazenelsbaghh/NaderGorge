export async function createLiveSupportRecording(chunks: Blob[]): Promise<File | undefined> {
  const recording = new Blob(chunks);
  const header = new Uint8Array(await recording.slice(0, 12).arrayBuffer());
  const signature = String.fromCharCode(...header);

  let extension: 'webm' | 'ogg' | 'mp4';
  if (signature.startsWith('\x1a\x45\xdf\xa3')) {
    extension = 'webm';
  } else if (signature.startsWith('OggS')) {
    extension = 'ogg';
  } else if (header.length === 12 && signature.slice(4, 8) === 'ftyp') {
    extension = 'mp4';
  } else {
    return undefined;
  }

  return new File([recording], `live-support-${Date.now()}.${extension}`, { type: `audio/${extension}` });
}
