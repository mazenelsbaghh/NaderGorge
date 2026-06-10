import { TelegramClient } from 'telegram';
import { StringSession } from 'telegram/sessions/index.js';
// @ts-ignore
import input from 'input';
import dotenv from 'dotenv';
import path from 'path';
import { fileURLToPath } from 'url';

const __filename = fileURLToPath(import.meta.url);
const __dirname = path.dirname(__filename);
dotenv.config({ path: path.resolve(__dirname, '../../../.env') });

const apiIdStr = process.env.TELEGRAM_API_ID;
const apiHash = process.env.TELEGRAM_API_HASH;

if (!apiIdStr || !apiHash) {
    console.error('Error: Please set TELEGRAM_API_ID and TELEGRAM_API_HASH in your .env file first.');
    process.exit(1);
}

const apiId = parseInt(apiIdStr, 10);
const stringSession = new StringSession(''); // starts a new session

(async () => {
    console.log('Connecting to Telegram...');
    const client = new TelegramClient(stringSession, apiId, apiHash, {
        connectionRetries: 5,
    });

    await client.start({
        phoneNumber: async () => await input.text('Please enter your phone number (+country_code...): '),
        password: async () => await input.text('Please enter your 2-step verification password (if any): '),
        phoneCode: async () => await input.text('Please enter the code you received: '),
        onError: (err) => console.error(err),
    });

    console.log('\n--- SUCCESS! Connected successfully ---');
    const sessionStr = client.session.save() as unknown as string;
    console.log('\nYour TELEGRAM_STRING_SESSION is:\n');
    console.log(sessionStr);
    console.log('\nCopy the exact session string above and paste it into your .env file.');
    
    await client.disconnect();
    process.exit(0);
})();
