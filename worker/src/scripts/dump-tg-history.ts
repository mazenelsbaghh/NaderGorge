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
    const botUsername = process.env.TELEGRAM_DOWNLOADER_BOT || 'utubebot';

    if (!apiIdStr || !apiHash || !stringSession) {
        console.error('Environment variables missing.');
        process.exit(1);
    }

    const apiId = parseInt(apiIdStr, 10);
    const client = new TelegramClient(new StringSession(stringSession), apiId, apiHash, {});
    await client.connect();

    console.log(`Fetching messages from bot @${botUsername}...`);
    try {
        const messages = await client.getMessages(botUsername, { limit: 10 });
        console.log(`Dumping last ${messages.length} messages:`);
        for (const msg of messages) {
            console.log(`- [${msg.id}] Outgoing: ${msg.out}, Text: "${msg.message || ''}", Media: ${!!msg.media}`);
            if (msg.media) {
                console.log(`  Media class: ${msg.media.className || typeof msg.media}`);
            }
            if (msg.replyMarkup) {
                console.log(`  Buttons:`, JSON.stringify(msg.replyMarkup, null, 2));
            }
        }
    } catch (err) {
        console.error('Failed to get messages:', err);
    }

    await client.disconnect();
    process.exit(0);
})();
