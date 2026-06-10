import dotenv from 'dotenv';
import path from 'path';
import { fileURLToPath } from 'url';
import { TelegramClient } from 'telegram';
import { StringSession } from 'telegram/sessions/index.js';

const __filename = fileURLToPath(import.meta.url);
const __dirname = path.dirname(__filename);
dotenv.config({ path: path.resolve(__dirname, '../../../.env') });

(async () => {
    const apiIdStr = process.env.TELEGRAM_API_ID;
    const apiHash = process.env.TELEGRAM_API_HASH;
    const stringSession = process.env.TELEGRAM_STRING_SESSION;

    if (!apiIdStr || !apiHash || !stringSession) {
        console.error('Environment variables missing.');
        process.exit(1);
    }

    const apiId = parseInt(apiIdStr, 10);
    const client = new TelegramClient(new StringSession(stringSession), apiId, apiHash, {});
    await client.connect();

    const bots = ['YTAudioBot', 'YtbAudioBot', 'convert_youtube_to_mp3_bot', 'u_download_bot', 'utubebot'];
    const testLink = 'https://www.youtube.com/watch?v=eT2g-twrG5c';

    for (const bot of bots) {
        console.log(`\n===================================`);
        console.log(`Testing bot: @${bot}`);
        console.log(`===================================`);
        try {
            const botEntity = await client.getEntity(bot);
            console.log(`Sending /start to @${bot}...`);
            await client.sendMessage(botEntity, { message: '/start' });
            await new Promise(r => setTimeout(r, 3000));

            console.log(`Sending link to @${bot}...`);
            await client.sendMessage(botEntity, { message: testLink });
            
            console.log(`Waiting 20 seconds for @${bot} response...`);
            await new Promise(r => setTimeout(r, 20000));

            const messages = await client.getMessages(botEntity, { limit: 5 });
            console.log(`Last 5 messages for @${bot}:`);
            for (const msg of messages) {
                console.log(`- [${msg.id}] Outgoing: ${msg.out}, Text: "${msg.message || ''}", Media: ${!!msg.media}`);
                if (msg.media) {
                    console.log(`  Media class: ${msg.media.className || typeof msg.media}`);
                }
                if (msg.replyMarkup) {
                    console.log(`  Buttons:`, JSON.stringify(msg.replyMarkup, null, 2));
                }
            }
        } catch (err: any) {
            console.error(`Error with @${bot}:`, err.message || err);
        }
    }

    await client.disconnect();
    process.exit(0);
})();
