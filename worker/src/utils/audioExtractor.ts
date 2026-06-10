import { execFile } from 'child_process';
import util from 'util';
import fs from 'fs';
import path from 'path';
import { fileURLToPath } from 'url';
import ytDlp from 'youtube-dl-exec';
import { TelegramClient } from 'telegram';
import { StringSession } from 'telegram/sessions/index.js';
import { NewMessage } from 'telegram/events/index.js';

const execFileAsync = util.promisify(execFile);
const ytDlpPath = (ytDlp as any).constants.YOUTUBE_DL_PATH;

// Resolve the worker root directory reliably from the compiled file location.
// dist/utils/ → ../../ = worker root
// src/utils/  → ../../ = worker root
const __filename = fileURLToPath(import.meta.url);
const __dirname  = path.dirname(__filename);
const workerRoot = path.resolve(__dirname, '../../');
const URL_PATTERN = /\bhttps?:\/\/[^\s]+/gi;

function sanitizeProcessOutput(value: unknown): string {
    const text = String(value || '').replace(URL_PATTERN, '[redacted-url]');
    return text.length > 500 ? `${text.substring(0, 500)}...` : text;
}

async function extractAudioViaTelegram(
    youtubeUrl: string,
    tempDir: string,
    outputFileName: string,
    expectedMp3: string
): Promise<boolean> {
    const apiIdStr = process.env.TELEGRAM_API_ID;
    const apiHash = process.env.TELEGRAM_API_HASH;
    const stringSession = process.env.TELEGRAM_STRING_SESSION;
    const botUsername = process.env.TELEGRAM_DOWNLOADER_BOT || 'utubebot';

    if (!apiIdStr || !apiHash || !stringSession) {
        console.log('[Telegram-DL] Telegram downloader is not fully configured (missing API ID, API Hash or Session).');
        return false;
    }

    const apiId = parseInt(apiIdStr, 10);
    if (isNaN(apiId)) {
        console.warn('[Telegram-DL] Invalid TELEGRAM_API_ID.');
        return false;
    }

    console.log(`[Telegram-DL] Connecting to Telegram client to download via @${botUsername}...`);
    const session = new StringSession(stringSession);
    const client = new TelegramClient(session, apiId, apiHash, {
        connectionRetries: 3,
    });

    try {
        await client.connect();
        console.log(`[Telegram-DL] Logged in successfully. Resolving bot entity: @${botUsername}`);

        const botEntity = await client.getEntity(botUsername);

        let audioMessage: any = null;
        let resolveMessage: (msg: any) => void;
        let rejectTimeout: (reason: Error) => void;

        const responsePromise = new Promise<any>((resolve, reject) => {
            resolveMessage = resolve;
            rejectTimeout = reject;
        });

        const handler = async (event: any) => {
            const message = event.message;
            if (!message || !message.senderId) return;

            try {
                const sender = await message.getSender();
                if (sender && sender.username && sender.username.toLowerCase() === botUsername.toLowerCase()) {
                    console.log(`[Telegram-DL] Incoming message from bot: "${message.message || ''}"`);
                    
                    if (message.media && (message.media.document || message.media.audio)) {
                        console.log('[Telegram-DL] Media document found in bot message.');
                        audioMessage = message;
                        resolveMessage(message);
                    }
                }
            } catch (err) {
                // ignore
            }
        };

        const eventBuilder = new NewMessage({ incoming: true });
        client.addEventHandler(handler, eventBuilder);

        console.log(`[Telegram-DL] Initializing chat by sending /start...`);
        await client.sendMessage(botEntity, { message: '/start' });
        await new Promise(resolve => setTimeout(resolve, 1500));

        console.log(`[Telegram-DL] Sending YouTube link: ${youtubeUrl}`);
        await client.sendMessage(botEntity, { message: youtubeUrl });

        // Wait up to 150 seconds for the bot to fetch, download, convert, and upload the audio back to us
        const timeoutId = setTimeout(() => {
            rejectTimeout(new Error('Timeout of 150s exceeded waiting for Telegram bot audio response.'));
        }, 150000);

        try {
            await responsePromise;
        } finally {
            clearTimeout(timeoutId);
            client.removeEventHandler(handler, eventBuilder);
        }

        if (!audioMessage || !audioMessage.media) {
            throw new Error('No audio media received.');
        }

        // Detect extension from document attributes
        let extension = 'mp3';
        const doc = audioMessage.media.document;
        if (doc && doc.attributes) {
            for (const attr of doc.attributes) {
                if (attr.className === 'DocumentAttributeFilename' && attr.fileName) {
                    const ext = path.extname(attr.fileName).replace('.', '');
                    if (ext) {
                        extension = ext.toLowerCase();
                    }
                    break;
                }
            }
        }

        const tempFile = path.join(tempDir, `${outputFileName}.downloaded.${extension}`);
        console.log(`[Telegram-DL] Downloading media to: ${tempFile}`);

        await client.downloadMedia(audioMessage.media, {
            outputFile: tempFile,
            workers: 4,
            progressCallback: (received: any, total: any) => {
                const totalBytes = Number(total || 0);
                const recBytes = Number(received || 0);
                const percent = totalBytes ? Math.round((recBytes / totalBytes) * 100) : 0;
                console.log(`[Telegram-DL] Download progress: ${recBytes}/${totalBytes} bytes (${percent}%)`);
            }
        } as any);

        if (!fs.existsSync(tempFile)) {
            throw new Error('Downloaded file was not created.');
        }

        if (extension === 'mp3') {
            fs.renameSync(tempFile, expectedMp3);
            console.log(`[Telegram-DL] Saved MP3 file directly.`);
        } else {
            console.log(`[Telegram-DL] Converting ${extension} to MP3 via ffmpeg...`);
            await execFileAsync('ffmpeg', [
                '-i', tempFile,
                '-vn',
                '-ar', '16000',
                '-ac', '1',
                '-b:a', '48k',
                '-y',
                expectedMp3
            ]);
            try { fs.unlinkSync(tempFile); } catch {}
            console.log(`[Telegram-DL] Successfully converted media to standard MP3.`);
        }

        return true;
    } catch (err: any) {
        console.error(`[Telegram-DL] Failed extraction step: ${err.message || err}`);
        throw err;
    } finally {
        try {
            await client.disconnect();
            console.log('[Telegram-DL] Disconnected client.');
        } catch (e) {}
    }
}

/**
 * Downloads a remote video and extracts a compressed MP3 audio track
 * for speech recognition by the Gemini API.
 *
 * Strategy:
 *   1. Try Telegram bot extraction first if environment variables are set.
 *   2. Try Cobalt API fallback.
 *   3. Try local yt-dlp extraction with cookies fallback.
 */
export async function extractAudioFromVideo(sourceUrl: string, outputFileName: string): Promise<string> {
    const tempDir = path.join(workerRoot, '.tmp');
    if (!fs.existsSync(tempDir)) {
        fs.mkdirSync(tempDir, { recursive: true });
    }

    // The canonical mp3 path we always want to end up with
    const expectedMp3 = path.join(tempDir, `${outputFileName}.mp3`);

    // Short-circuit: if a previous attempt already produced the file, reuse it
    if (fs.existsSync(expectedMp3)) {
        console.log(`[Youtube-DL] Reusing existing audio: ${expectedMp3}`);
        return expectedMp3;
    }

    console.log(`[Youtube-DL] Starting extraction for job ${outputFileName}`);
    console.log(`[Youtube-DL] Source received (${sourceUrl.length} chars).`);
    console.log(`[Youtube-DL] Worker root: ${workerRoot}`);
    console.log(`[Youtube-DL] Temp dir:    ${tempDir}`);

    // Normalise raw YouTube IDs → full URL
    let url = sourceUrl;
    if (url.length === 11 && !url.includes('http')) {
        url = `https://www.youtube.com/watch?v=${url}`;
    }

    // ── Try Telegram Downloader First (If fully configured) ──
    const apiIdStr = process.env.TELEGRAM_API_ID;
    const apiHash = process.env.TELEGRAM_API_HASH;
    const stringSession = process.env.TELEGRAM_STRING_SESSION;
    if (apiIdStr && apiHash && stringSession) {
        try {
            console.log(`[Youtube-DL] Attempting Telegram bot download for: ${url}`);
            const tgSuccess = await extractAudioViaTelegram(url, tempDir, outputFileName, expectedMp3);
            if (tgSuccess && fs.existsSync(expectedMp3)) {
                console.log(`[Youtube-DL] ✅ Successfully extracted audio via Telegram bot!`);
                return expectedMp3;
            }
        } catch (tgErr: any) {
            console.warn(`[Youtube-DL] Telegram bot download failed: ${tgErr.message || tgErr}. Proceeding to fallbacks...`);
        }
    }

    // ── Try Cobalt API First (Bypasses YouTube bot block without cookies/local binaries) ──
    try {
        console.log(`[Youtube-DL] Attempting extraction via Cobalt API for: ${url}`);
        const cobaltRes = await fetch('https://api.cobalt.tools/api/json', {
            method: 'POST',
            headers: {
                'Accept': 'application/json',
                'Content-Type': 'application/json',
            },
            body: JSON.stringify({
                url,
                isAudioOnly: true,
                aFormat: 'mp3',
            }),
        });

        if (!cobaltRes.ok) {
            throw new Error(`Cobalt returned status ${cobaltRes.status}`);
        }

        const data = (await cobaltRes.json()) as { url?: string; status?: string; text?: string };
        if (data.status === 'error' || !data.url) {
            throw new Error(data.text || 'Cobalt API did not return a valid download URL.');
        }

        console.log(`[Youtube-DL] Cobalt returned direct audio URL. Downloading...`);
        const fileResponse = await fetch(data.url);
        if (!fileResponse.ok) {
            throw new Error(`Failed to download audio file: ${fileResponse.statusText}`);
        }

        const fileStream = fs.createWriteStream(expectedMp3);
        const { Readable } = await import('stream');
        const { finished } = await import('stream/promises');
        await finished(Readable.fromWeb(fileResponse.body as any).pipe(fileStream));

        console.log(`[Youtube-DL] ✅ Audio downloaded successfully via Cobalt: ${expectedMp3}`);
        return expectedMp3;
    } catch (cobaltErr: any) {
        console.warn(`[Youtube-DL] Cobalt API failed (${cobaltErr.message || cobaltErr}). Falling back to local yt-dlp...`);
    }

    // ── Snapshot BEFORE ──────────────────────────────────────────────────────
    const filesBefore = new Set(fs.readdirSync(tempDir));

    // ── Run yt-dlp ──────────────────────────────────────────────────────────
    // Do NOT pass .mp3 extension in the -o template: yt-dlp appends the right
    // extension itself. Passing '.mp3' causes double-extension bugs (uuid.mp3.mp3).
    // Also: remove --no-warnings so errors surface in stderr.
    const outputTemplate = path.join(tempDir, outputFileName);
    const cookiesPaths = [
        path.join(workerRoot, 'cookies.txt'),
        path.join(workerRoot, 'host-worker/cookies.txt'),
        '/backend/src/NaderGorge.API/wwwroot/cookies.txt'
    ];
    const cookiesPath = cookiesPaths.find(p => fs.existsSync(p));

    const args = [
        url,
        '--extract-audio',
        '--audio-format',  'mp3',
        '--audio-quality', '5',
        '-o',              outputTemplate,
        '--no-check-certificates',
        '--no-playlist',
        '--newline',
        // yt-dlp 2026+ requires a JS runtime to process YouTube's player API.
        // Pointing it to the system Node binary fixes the "no JS runtime" warning
        // that causes all YouTube formats to be invisible.
        '--js-runtimes', `node:${process.execPath}`,
    ];

    if (cookiesPath) {
        args.push('--cookies', cookiesPath);
        console.log(`[Youtube-DL] Found cookies.txt at ${cookiesPath}, passing to yt-dlp.`);
    }

    console.log(`[Youtube-DL] Running yt-dlp...`);
    try {
        const { stdout, stderr } = await execFileAsync(ytDlpPath, args);
        if (stdout) console.log(`[Youtube-DL] stdout:\n${sanitizeProcessOutput(stdout)}`);
        if (stderr) console.warn(`[Youtube-DL] stderr:\n${sanitizeProcessOutput(stderr)}`);
    } catch (err: any) {
        const errorMsg = err.stderr || err.stdout || err.message || String(err);
        const safeErrorMsg = sanitizeProcessOutput(errorMsg);
        console.error(`[Youtube-DL] yt-dlp exited with error:\n${safeErrorMsg}`);
        throw new Error(`yt-dlp failed to download. Detail: ${safeErrorMsg}`);
    }

    // ── Check expected path first ─────────────────────────────────────────────
    if (fs.existsSync(expectedMp3)) {
        console.log(`[Youtube-DL] ✅ Audio ready at expected path: ${expectedMp3}`);
        return expectedMp3;
    }

    // ── Snapshot AFTER and find new files ────────────────────────────────────
    const filesAfter  = fs.readdirSync(tempDir);
    const newFiles    = filesAfter.filter(f => !filesBefore.has(f));
    console.log(`[Youtube-DL] New files in .tmp after yt-dlp:`, newFiles);

    if (newFiles.length === 0) {
        // yt-dlp exited 0 but produced nothing — likely a format/URL issue
        throw new Error(
            `yt-dlp ran successfully but created no output file. ` +
            `URL may be unsupported or private.`
        );
    }

    // ── Find the best candidate (prefer .mp3, else first new file) ───────────
    let candidate: string | undefined =
                   newFiles.find(f => f.endsWith('.mp3'))
                ?? newFiles.find(f => f.startsWith(outputFileName))
                ?? newFiles[0];

    if (!candidate) {
        throw new Error(
            `yt-dlp ran but produced no usable file. New files seen: ${newFiles.join(', ')}`
        );
    }

    const candidatePath = path.join(tempDir, candidate);

    if (candidate.endsWith('.mp3')) {
        // It's already an mp3 — just rename to the canonical name if needed
        if (candidatePath !== expectedMp3) {
            fs.renameSync(candidatePath, expectedMp3);
            console.log(`[Youtube-DL] Renamed "${candidate}" → "${outputFileName}.mp3"`);
        }
        return expectedMp3;
    }

    // ── Convert non-mp3 to mp3 via ffmpeg ────────────────────────────────────
    console.log(`[Youtube-DL] File is not .mp3 ("${candidate}") — converting with ffmpeg...`);
    try {
        await execFileAsync('ffmpeg', [
            '-i',  candidatePath,
            '-vn',                   // strip video
            '-ar', '16000',          // 16kHz — optimal for Gemini speech recognition
            '-ac', '1',              // mono
            '-b:a', '48k',           // low bitrate — speech only
            '-y',                    // overwrite
            expectedMp3,
        ]);
        // Cleanup the intermediate file
        try { fs.unlinkSync(candidatePath); } catch { /* ignore */ }
        console.log(`[Youtube-DL] ✅ Converted to mp3: ${expectedMp3}`);
        return expectedMp3;
    } catch (ffmpegErr: any) {
        const msg = ffmpegErr.stderr || ffmpegErr.message || String(ffmpegErr);
        throw new Error(`ffmpeg conversion failed: ${msg}`);
    }
}
