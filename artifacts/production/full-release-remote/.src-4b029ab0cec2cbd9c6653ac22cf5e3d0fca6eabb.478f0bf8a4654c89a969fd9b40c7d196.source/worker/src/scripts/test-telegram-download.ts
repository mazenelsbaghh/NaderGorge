import dotenv from 'dotenv';
import path from 'path';
import { fileURLToPath } from 'url';
import { extractAudioFromVideo } from '../utils/audioExtractor.js';

const __filename = fileURLToPath(import.meta.url);
const __dirname = path.dirname(__filename);
dotenv.config({ path: path.resolve(__dirname, '../../../.env') });

(async () => {
    const testVideo = 'eT2g-twrG5c';
    console.log(`Starting test extraction for YouTube ID: ${testVideo}`);
    try {
        const result = await extractAudioFromVideo(testVideo, 'test-tg-output-' + Date.now());
        console.log(`TEST SUCCESSFUL! File ready at: ${result}`);
    } catch (err: any) {
        console.error('TEST FAILED:', err);
    }
})();
