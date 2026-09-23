import fs from 'node:fs/promises';
import {parseEnv} from 'node:util';
import {GoogleGenAI} from '../../worker/node_modules/@google/genai/dist/node/index.mjs';
const directory = new URL('./',import.meta.url);
const env = parseEnv(await fs.readFile(new URL('../../worker/.env',directory),'utf8'));
const client = new GoogleGenAI({apiKey:env.GEMINI_API_KEY,httpOptions:{timeout:120000}});
async function alignVoice(name) {
 const audio = await fs.readFile(new URL(`gemini-${name}.mp3`,directory));
 const response = await client.models.generateContent({
  model: env.AI_TEXT_MODEL || 'gemini-3.6-flash',
  contents:[{inlineData:{mimeType:'audio/mpeg',data:audio.toString('base64')}},{text:'Listen carefully to this generated Egyptian Arabic welcome. Return JSON with a segments array: {start: seconds as a number, end: seconds as a number, text: verbatim Arabic}. Split exactly at these 5 phrases: "أهو كده، مسار نوّرت تاني!", "وحشتني يا صاحبي.", "جاهز نكمّل من مكان ما وقفنا؟", "خطوة صغيرة النهارده، تقرّبك من حلمك.", "يلا بينا!". Use actual timestamps from the attached recording, not word count estimates. Also add a short quality field describing audible language/dialect, clarity and any omitted or added text. No extra markdown.'}],
  config:{responseMimeType:'application/json'},
 });
 const alignment=JSON.parse(response.text);
 if(!Array.isArray(alignment.segments)||alignment.segments.length!==5)throw new Error('Unexpected alignment segments');
 await fs.writeFile(new URL(`gemini-${name}.json`,directory),JSON.stringify(alignment,null,2));
 console.log(name,JSON.stringify(alignment));
}
try{await Promise.all(['return-charon'].map(alignVoice));}
catch(error){console.error(String(error.message).replaceAll(env.GEMINI_API_KEY,'[redacted]').slice(0,400));process.exitCode=1;}
