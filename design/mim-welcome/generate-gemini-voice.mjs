import fs from 'node:fs/promises';
import { parseEnv } from 'node:util';
import { GoogleGenAI } from '../../worker/node_modules/@google/genai/dist/node/index.mjs';

const directory = new URL('./', import.meta.url);
const env = parseEnv(await fs.readFile(new URL('../../worker/.env', directory), 'utf8'));
const client = new GoogleGenAI({ apiKey: env.GEMINI_API_KEY, httpOptions: { timeout: 180000 } });
const transcript = 'إيه ده! إنت وصلت؟ نوّرت مسار! أنا ميم، وصاحبك في رحلة التعلّم. هنفهم، ونجرّب، ونحلّ سوا. جاهز؟ يلا نبدأ أول خطوة! أشوفك جوّه!';
const direction = `Perform this script as Meem, an adorable friendly adventure mascot welcoming an Egyptian secondary-school student. Speak authentic conversational CAIRO EGYPTIAN ARABIC, never Modern Standard Arabic. Youthful male voice with a smile, warm and playful, lively yet natural, not a narrator or advertisement, not a toddler and not squeaky. Express delighted surprise in the opening, smile on the welcome, reassuring friendship in the middle, cheerful invitation and a brief friendly goodbye at the end. Natural conversational rhythm, small expressive pauses, about 15 seconds. Pronounce ميم as Meem (long ee) and مسار as Masaar. Read ONLY the Arabic script below, with no extra words, no music, no sound effects, no English:\n\n${transcript}`;

async function generateVoice(voiceName) {
  const response = await client.models.generateContent({
    model: 'gemini-2.5-pro-preview-tts',
    contents: direction,
    config: { responseModalities: ['AUDIO'], speechConfig: { voiceConfig: { prebuiltVoiceConfig: { voiceName } } } },
  });
  const audioPart = response.candidates?.[0]?.content?.parts?.find(part => part.inlineData?.mimeType?.startsWith('audio/'));
  if (!audioPart?.inlineData?.data) throw new Error(`No audio returned for ${voiceName}`);
  const filename = `gemini-${voiceName.toLowerCase()}.pcm`;
  await fs.writeFile(new URL(filename, directory), Buffer.from(audioPart.inlineData.data, 'base64'));
  console.log(JSON.stringify({ filename, mimeType: audioPart.inlineData.mimeType }));
}
for (const voiceName of ['Puck', 'Charon']) {
  try { await generateVoice(voiceName); }
  catch (error) { console.error(JSON.stringify({ voiceName, status: error.status ?? 'failed', message: String(error.message).replaceAll(env.GEMINI_API_KEY, '[redacted]').slice(0,450) })); process.exitCode = 1; break; }
}
